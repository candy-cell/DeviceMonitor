namespace DeviceMonitor.Core.Enums;

/// <summary>
/// 报警规则类型。
/// </summary>
public enum AlarmType
{
    /// <summary>上限报警：值 &gt; 阈值触发。</summary>
    High = 0,

    /// <summary>下限报警：值 &lt; 阈值触发。</summary>
    Low = 1,

    /// <summary>上上限报警（严重级）：值 &gt; 阈值触发。</summary>
    HighHigh = 2,

    /// <summary>下下限报警（严重级）：值 &lt; 阈值触发。</summary>
    LowLow = 3,

    /// <summary>变化率报警：单位时间内变化量超限。</summary>
    RateOfChange = 4
}

/// <summary>
/// 报警等级。用于界面着色与提示方式。
/// </summary>
public enum AlarmLevel
{
    /// <summary>提示（蓝）。</summary>
    Info = 1,

    /// <summary>警告（黄）。</summary>
    Warning = 2,

    /// <summary>严重（红）。</summary>
    Critical = 3
}

/// <summary>
/// 报警状态机。
/// 注意：Ack（已确认）与 Clear（已恢复）是两件独立的事——
/// 确认不代表恢复，恢复也不代表已确认。
/// </summary>
public enum AlarmState
{
    /// <summary>激活中（条件仍满足，未确认）。</summary>
    Active = 0,

    /// <summary>已确认（操作员已处理，但条件可能仍在持续）。</summary>
    Acknowledged = 1,

    /// <summary>已恢复（条件已消失）。</summary>
    Cleared = 2
}
