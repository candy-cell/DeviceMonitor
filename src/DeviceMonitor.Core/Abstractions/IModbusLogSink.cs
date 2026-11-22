namespace DeviceMonitor.Core.Abstractions;

/// <summary>
/// 通信日志级别。
/// </summary>
public enum ModbusLogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

/// <summary>
/// 结构化通信日志条目。
/// </summary>
/// <remarks>
/// 刻意做成对象而不是拼接字符串，好处是：
/// 可以按从站地址筛、按功能码筛、按等级上色、序列化进数据库，
/// 而不是只能往控制台打一行文本。
/// </remarks>
public sealed class ModbusLogEntry
{
    /// <summary>时间戳。</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>级别。</summary>
    public ModbusLogLevel Level { get; init; } = ModbusLogLevel.Info;

    /// <summary>分类，如 Connect / Read / Write / Heartbeat / Poll / Reconnect。</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>正文。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>关联异常（可为空）。</summary>
    public Exception? Exception { get; init; }

    /// <summary>从站地址（可为空，连接类日志无从站）。</summary>
    public byte? SlaveId { get; init; }

    /// <summary>功能码（可为空，非读写类日志无功能码）。</summary>
    public byte? FunctionCode { get; init; }

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss.fff}][{Level}][{Category}]" +
        (SlaveId.HasValue ? $"[slave={SlaveId}]" : string.Empty) +
        (FunctionCode.HasValue ? $"[fc=0x{FunctionCode:X2}]" : string.Empty) +
        $" {Message}" +
        (Exception is null ? string.Empty : $" | {Exception.GetType().Name}: {Exception.Message}");
}

/// <summary>
/// 日志出口。由使用方决定怎么落（文件 / 数据库 / UI 面板）。
/// </summary>
public interface IModbusLogSink
{
    /// <summary>写一条日志。</summary>
    void Write(ModbusLogEntry entry);
}
