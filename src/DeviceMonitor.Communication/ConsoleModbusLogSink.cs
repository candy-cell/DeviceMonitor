using DeviceMonitor.Core.Abstractions;

namespace DeviceMonitor.Communication;

/// <summary>
/// 把通信日志打到控制台的简单实现。开发调试时用。
/// </summary>
/// <remarks>
/// 正式环境应替换为写文件 / 写数据库 / 推送到 UI 面板的实现。
/// </remarks>
public sealed class ConsoleModbusLogSink : IModbusLogSink
{
    /// <summary>低于该级别的日志直接丢弃。</summary>
    public ModbusLogLevel MinLevel { get; set; } = ModbusLogLevel.Debug;

    /// <inheritdoc />
    public void Write(ModbusLogEntry entry)
    {
        if (entry is null || entry.Level < MinLevel) return;

        ConsoleColor old = Console.ForegroundColor;
        Console.ForegroundColor = entry.Level switch
        {
            ModbusLogLevel.Debug => ConsoleColor.Gray,
            ModbusLogLevel.Info => ConsoleColor.White,
            ModbusLogLevel.Warn => ConsoleColor.Yellow,
            ModbusLogLevel.Error => ConsoleColor.Red,
            _ => old
        };

        Console.WriteLine(entry.ToString());
        Console.ForegroundColor = old;
    }
}
