using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Core.Abstractions;

/// <summary>
/// 设备通信客户端抽象。
/// </summary>
/// <remarks>
/// 这一层是整个架构的关键：上面的轮询引擎只认这个接口，
/// 完全不关心底下是串口（RTU）还是网口（TCP）。
/// 将来要加 S7 / OPC UA，只要再实现一个 <see cref="IDeviceClient"/> 即可，上层代码一行都不用改。
///
/// 为什么用同步方法而不是 async？
/// Modbus 在 RS485 半双工总线上本来就是"一问一答、顺序执行"，
/// 采集线程跑同步调用最直观；需要不阻塞 UI 时，由上层用 Task.Run 包一层即可。
/// </remarks>
public interface IDeviceClient : IDisposable
{
    /// <summary>连接是否已打开。</summary>
    bool IsOpen { get; }

    /// <summary>从站地址。</summary>
    byte SlaveId { get; }

    /// <summary>打开连接。返回 false 表示失败（具体原因见日志）。</summary>
    bool Open();

    /// <summary>关闭连接并释放资源。</summary>
    void Close();

    /// <summary>
    /// 读取寄存器（字，16 位）。根据 <paramref name="area"/> 自动选择功能码：
    /// 3 区用 0x04，4 区用 0x03。
    /// </summary>
    ushort[] ReadRegisters(ModbusArea area, ushort startAddress, ushort count);

    /// <summary>
    /// 读取位（线圈 / 离散输入）。根据 <paramref name="area"/> 选择功能码：
    /// 0 区用 0x01，1 区用 0x02。
    /// </summary>
    bool[] ReadBits(ModbusArea area, ushort startAddress, ushort count);

    /// <summary>写单个保持寄存器（功能码 0x06）。</summary>
    void WriteSingleRegister(ushort address, ushort value);

    /// <summary>写多个保持寄存器（功能码 0x10）。</summary>
    void WriteMultipleRegisters(ushort startAddress, ushort[] values);

    /// <summary>写单个线圈（功能码 0x05）。</summary>
    void WriteSingleCoil(ushort address, bool value);

    /// <summary>写多个线圈（功能码 0x0F）。</summary>
    void WriteMultipleCoils(ushort startAddress, bool[] values);

    /// <summary>
    /// 心跳探测：读 0 号保持寄存器 1 个。
    /// 只要从站有响应（哪怕是异常码），就判定为在线。
    /// </summary>
    bool ProbeOnline();

    /// <summary>
    /// 每次心跳都会触发。适合 UI 实时刷新在线状态指示。
    /// 参数：(从站地址, 是否在线)。
    /// </summary>
    event Action<byte, bool>? OnHeartbeatResult;

    /// <summary>
    /// 仅在在线状态发生跃变时触发。适合写告警日志，避免高频刷屏。
    /// 参数：(从站地址, 是否在线)。
    /// </summary>
    event Action<byte, bool>? OnSlaveStatusChanged;
}
