using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Demo;

/// <summary>
/// 假设备客户端工厂：总是返回 FakeDeviceClient。
/// </summary>
public sealed class FakeDeviceClientFactory : IDeviceClientFactory
{
    public IDeviceClient Create(Device device, IModbusLogSink? logSink = null)
        => new FakeDeviceClient(device);
}
