using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;

namespace DeviceMonitor.Communication;

/// <summary>
/// 模式可切换的工厂：根据 <see cref="IsSimulation"/> 决定返回真客户端还是假客户端。
/// </summary>
/// <remarks>
/// 与写死的 <see cref="DeviceClientFactory"/> / <see cref="SimulatedDeviceClientFactory"/> 不同，
/// 这个工厂在<b>运行时</b>就能切换，无需重新注册 DI 或重启程序。
///
/// 用途：界面上放一个"模拟/真实"开关，用户点一下就切换，不用改代码重新编译。
/// </remarks>
public sealed class SwitchableDeviceClientFactory : IDeviceClientFactory
{
    private readonly IDeviceClientFactory _real = new DeviceClientFactory();
    private readonly IDeviceClientFactory _fake = new SimulatedDeviceClientFactory();

    /// <summary>当前是否模拟模式。true=假设备，false=真串口/网口。</summary>
    public static bool IsSimulation { get; set; } = true;

    public IDeviceClient Create(Device device, IModbusLogSink? logSink = null)
        => IsSimulation ? _fake.Create(device, logSink) : _real.Create(device, logSink);
}
