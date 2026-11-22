using DeviceMonitor.Core.Entities;

namespace DeviceMonitor.Core.Abstractions;

/// <summary>
/// 设备客户端工厂。根据设备的 <see cref="Device.Protocol"/> 创建对应的实现。
/// </summary>
public interface IDeviceClientFactory
{
    /// <summary>
    /// 按设备配置创建客户端。注意：创建后连接并未打开，需调用 <see cref="IDeviceClient.Open"/>。
    /// </summary>
    /// <param name="device">设备配置。</param>
    /// <param name="logSink">日志出口，可为 null（表示不记录）。</param>
    IDeviceClient Create(Device device, IModbusLogSink? logSink = null);
}
