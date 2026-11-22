using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;

namespace DeviceMonitor.Communication;

/// <summary>
/// 模拟模式工厂：总是返回 <see cref="FakeDeviceClient"/>，不碰任何硬件。
/// </summary>
/// <remarks>
/// 与 <see cref="DeviceClientFactory"/>（真串口/真网口）形成一对，由配置开关决定用哪个。
/// 上层代码（AcquisitionService / PollingEngine）完全无感——它们只认 IDeviceClientFactory。
/// </remarks>
public sealed class SimulatedDeviceClientFactory : IDeviceClientFactory
{
    public IDeviceClient Create(Device device, IModbusLogSink? logSink = null)
        => new FakeDeviceClient(device);
}
