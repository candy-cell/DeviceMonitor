using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Alarms;

/// <summary>
/// 报警引擎：把"原始测点值"转换成"报警状态机"的纯内存逻辑。
/// </summary>
/// <remarks>
/// 设计目标：<b>不碰硬件、不碰数据库、不碰 UI</b>。
/// 它只做三件事：判定阈值 → 维护状态机 → 通过事件把结果抛给外部（外部再决定落库还是刷新界面）。
///
/// 判定要经过三层过滤，否则现场会被"报警风暴"淹没：
/// <list type="number">
/// <item><b>阈值判断</b>：值是否越过规则阈值。</item>
/// <item><b>死区（Deadband）防抖</b>：超过阈值才进入报警区间；但要恢复到
///     "阈值 ± 死区"之外才解除，避免值在阈值附近抖动导致反复报警。</item>
/// <item><b>延时（DelayMs）确认</b>：条件连续满足超过该时长才真正产生报警记录，过滤瞬时毛刺。</item>
/// </list>
///
/// 状态机（每个规则独立维护）：
/// <code>
/// Normal ──条件满足且持续 DelayMs──► Active ──操作员确认──► Acknowledged
///    ▲                                 │        │
///    └────────────条件恢复（Cleared）────┴────────┘
/// </code>
/// 注意：Ack（确认）与 Clear（恢复）是两件独立的事——确认不代表恢复，恢复也不代表已确认。
/// </remarks>
public sealed class AlarmEngine
{
    private readonly object _lock = new();
    private readonly Dictionary<int, RuleRuntime> _states = new();

    /// <summary>触发新报警时抛出（状态 = Active）。</summary>
    public event Action<AlarmRecord>? Triggered;

    /// <summary>报警被确认时抛出（状态 = Acknowledged）。</summary>
    public event Action<AlarmRecord>? Acknowledged;

    /// <summary>报警恢复时抛出（状态 = Cleared，含 EndTime 与峰值）。</summary>
    public event Action<AlarmRecord>? Cleared;

    /// <summary>
    /// 喂入一个测点值，驱动该规则的状态机。
    /// </summary>
    /// <param name="rule">报警规则（每个规则的 Id 必须唯一，用作状态字典的 key）。</param>
    /// <param name="value">测点当前工程值。</param>
    /// <param name="now">采样时刻。</param>
    public void Evaluate(AlarmRule rule, double value, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(rule);

        lock (_lock)
        {
            var state = GetOrCreate(rule.Id);
            bool armed = ComputeArmed(state.Armed, rule, value);

            if (armed)
            {
                if (state.Current is null)
                {
                    // 刚进入报警区间，开始计时
                    state.FirstArmedAt ??= now;

                    // 延时确认：条件持续满足超过 DelayMs 才真正报警
                    if (now - state.FirstArmedAt.Value >= TimeSpan.FromMilliseconds(rule.DelayMs))
                    {
                        var record = CreateRecord(rule, value, now);
                        state.Current = record;
                        Triggered?.Invoke(record);
                    }
                }
                else
                {
                    // 报警持续中（Active 或 Acknowledged），更新峰值
                    UpdatePeak(state.Current, rule, value);
                }
            }
            else
            {
                // 离开报警区间，重置计时
                state.FirstArmedAt = null;

                if (state.Current is not null && state.Current.State != AlarmState.Cleared)
                {
                    var record = state.Current;
                    record.EndTime = now;
                    record.State = AlarmState.Cleared;
                    state.Current = null;
                    Cleared?.Invoke(record);
                }
            }

            state.Armed = armed;
        }
    }

    /// <summary>
    /// 确认一条当前处于 Active 状态的报警。
    /// </summary>
    /// <returns>是否成功确认（找不到 Active 记录时返回 false）。</returns>
    public bool Acknowledge(int ruleId, string user, DateTime now)
    {
        lock (_lock)
        {
            if (_states.TryGetValue(ruleId, out var state) &&
                state.Current is { State: AlarmState.Active } record)
            {
                record.State = AlarmState.Acknowledged;
                record.AckTime = now;
                record.AckUser = user;
                Acknowledged?.Invoke(record);
                return true;
            }
            return false;
        }
    }

    /// <summary>获取指定规则当前活跃的记录（Active 或 Acknowledged），无则返回 null。</summary>
    public AlarmRecord? GetCurrent(int ruleId)
    {
        lock (_lock)
        {
            return _states.TryGetValue(ruleId, out var s) ? s.Current : null;
        }
    }

    /// <summary>获取全部活跃记录（Active 或 Acknowledged）。</summary>
    public IReadOnlyList<AlarmRecord> GetActive()
    {
        lock (_lock)
        {
            return _states.Values
                .Where(s => s.Current is not null && s.Current.State != AlarmState.Cleared)
                .Select(s => s.Current!)
                .ToList();
        }
    }

    /// <summary>重置某规则的状态（例如规则被禁用、配置变更时）。</summary>
    public void Reset(int ruleId)
    {
        lock (_lock)
        {
            if (_states.TryGetValue(ruleId, out var s))
            {
                s.Armed = false;
                s.FirstArmedAt = null;
                s.Current = null;
            }
        }
    }

    // ==================== 内部实现 ====================

    private RuleRuntime GetOrCreate(int ruleId)
    {
        if (!_states.TryGetValue(ruleId, out var state))
        {
            state = new RuleRuntime();
            _states[ruleId] = state;
        }
        return state;
    }

    /// <summary>
    /// 死区滞回比较：进入区间与退出区间用两套阈值，中间地带维持原状态。
    /// </summary>
    private static bool ComputeArmed(bool currentlyArmed, AlarmRule rule, double value)
    {
        bool arm, disarm;

        switch (rule.Type)
        {
            case AlarmType.High:
            case AlarmType.HighHigh:
                arm = value > rule.Threshold;
                disarm = value <= rule.Threshold - rule.Deadband;
                break;

            case AlarmType.Low:
            case AlarmType.LowLow:
                arm = value < rule.Threshold;
                disarm = value >= rule.Threshold + rule.Deadband;
                break;

            case AlarmType.RateOfChange:
                throw new NotSupportedException(
                    $"报警类型 {rule.Type}（变化率）暂未实现，下一步补充。");

            default:
                throw new ArgumentOutOfRangeException(nameof(rule), rule.Type, "未知的报警类型。");
        }

        if (arm) return true;
        if (disarm) return false;
        return currentlyArmed;  // 死区内：维持原状态
    }

    private static AlarmRecord CreateRecord(AlarmRule rule, double value, DateTime now)
    {
        return new AlarmRecord
        {
            AlarmRuleId = rule.Id,
            DataPointId = rule.DataPointId,
            StartTime = now,
            PeakValue = value,
            State = AlarmState.Active,
            Level = rule.Level,
            Message = BuildMessage(rule, value)
        };
    }

    /// <summary>
    /// 更新峰值：High 类取最大值，Low 类取最小值（都朝"更严重"的方向）。
    /// </summary>
    private static void UpdatePeak(AlarmRecord record, AlarmRule rule, double value)
    {
        if (rule.Type is AlarmType.High or AlarmType.HighHigh)
        {
            record.PeakValue = Math.Max(record.PeakValue, value);
        }
        else
        {
            record.PeakValue = Math.Min(record.PeakValue, value);
        }
    }

    private static string BuildMessage(AlarmRule rule, double value)
    {
        string type = rule.Type switch
        {
            AlarmType.High => "超上限",
            AlarmType.Low => "低下限",
            AlarmType.HighHigh => "超上上限",
            AlarmType.LowLow => "低下下限",
            _ => rule.Type.ToString()
        };
        return $"测点[{rule.DataPointId}] {type}：阈值 {rule.Threshold}，当前 {value:F2}";
    }

    /// <summary>每个规则一份的运行时状态。</summary>
    private sealed class RuleRuntime
    {
        /// <summary>是否处于"报警条件成立"区间（含死区滞回）。</summary>
        public bool Armed;

        /// <summary>条件首次满足的时刻（用于延时确认）。</summary>
        public DateTime? FirstArmedAt;

        /// <summary>当前活跃的报警记录；恢复后置 null。</summary>
        public AlarmRecord? Current;
    }
}
