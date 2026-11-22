using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Core.Entities;

/// <summary>
/// 报警规则。一个测点可以配多条规则（上限 / 下限 / 严重级 / 变化率）。
/// </summary>
/// <remarks>
/// 判定要经过三层过滤，否则现场会被"报警风暴"淹没：
/// ① 阈值判断 → ② 死区（Deadband）防抖 → ③ 延时（DelayMs）确认。
/// </remarks>
public class AlarmRule
{
    public int Id { get; set; }

    /// <summary>关联测点 Id。</summary>
    public int DataPointId { get; set; }

    /// <summary>关联测点（导航属性）。</summary>
    public DataPoint? DataPoint { get; set; }

    /// <summary>规则类型。</summary>
    public AlarmType Type { get; set; } = AlarmType.High;

    /// <summary>报警阈值。</summary>
    public double Threshold { get; set; }

    /// <summary>
    /// 死区（回差）。以 High 规则为例：值超过 Threshold 触发；
    /// 但要恢复到"低于 Threshold - Deadband"才算解除，避免值在阈值附近抖动导致反复报警。
    /// </summary>
    public double Deadband { get; set; }

    /// <summary>延时确认（毫秒）。条件连续满足超过该时长才真正产生报警，用于过滤瞬时毛刺。</summary>
    public int DelayMs { get; set; }

    /// <summary>报警等级。</summary>
    public AlarmLevel Level { get; set; } = AlarmLevel.Warning;

    /// <summary>是否启用。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>规则说明（界面上给人看）。</summary>
    public string? Remark { get; set; }
}
