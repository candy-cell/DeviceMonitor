using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Core.Entities;

/// <summary>
/// 报警记录。一条记录对应一次"从触发到恢复"的完整过程。
/// </summary>
/// <remarks>
/// <see cref="AlarmState"/>、<see cref="AckTime"/>、<see cref="EndTime"/> 三者相互独立：
/// 确认（Ack）不代表恢复（End），恢复也不代表已确认。
/// </remarks>
public class AlarmRecord
{
    public long Id { get; set; }

    /// <summary>触发的规则 Id。</summary>
    public int AlarmRuleId { get; set; }

    /// <summary>关联测点 Id（冗余存储，方便按测点查询）。</summary>
    public int DataPointId { get; set; }

    /// <summary>触发时刻。</summary>
    public DateTime StartTime { get; set; }

    /// <summary>恢复时刻。为 null 表示尚未恢复。</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>报警期间出现的峰值（用于事后判断严重程度）。</summary>
    public double PeakValue { get; set; }

    /// <summary>当前状态。</summary>
    public AlarmState State { get; set; } = AlarmState.Active;

    /// <summary>确认时刻。</summary>
    public DateTime? AckTime { get; set; }

    /// <summary>确认人。</summary>
    public string? AckUser { get; set; }

    /// <summary>报警等级（从规则复制，便于查询时不必再关联规则表）。</summary>
    public AlarmLevel Level { get; set; } = AlarmLevel.Warning;

    /// <summary>报警文本，如"1#温湿度.温度 超上限 35℃（当前 37.2℃）"。</summary>
    public string Message { get; set; } = string.Empty;
}
