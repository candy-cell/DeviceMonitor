using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Communication;

/// <summary>
/// 设备客户端工厂。按 <see cref="Device.Protocol"/> 返回对应实现。
/// </summary>
/// <remarks>
/// 这是"可扩展协议"的落点：将来加 Modbus TCP / S7 / OPC UA，
/// 只需在这里多一个 case，上层轮询引擎完全不用改。
/// </remarks>
public sealed class DeviceClientFactory : IDeviceClientFactory
{
    /// <inheritdoc />
    public IDeviceClient Create(Device device, IModbusLogSink? logSink = null)
    {
        ArgumentNullException.ThrowIfNull(device);

        return device.Protocol switch
        {
            DeviceProtocol.Rtu => new ModbusRtuClient(device, logSink),
            DeviceProtocol.Tcp => new ModbusTcpClient(device, logSink),
            _ => throw new ArgumentOutOfRangeException(
                nameof(device), device.Protocol, "未知的协议类型。")
        };
    }
}
