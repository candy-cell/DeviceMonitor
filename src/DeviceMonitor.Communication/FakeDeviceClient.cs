using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Communication;

/// <summary>
/// 模拟设备客户端：不碰任何真实硬件，返回带波动的温湿度数据。
/// </summary>
/// <remarks>
/// 这是「IDeviceClient 抽象」的最大价值——用假实现替换真串口，
/// 让 AcquisitionService / PollingEngine / 报警 / UI 全链路在无硬件下跑通。
///
/// 数据特征：
/// - 地址 0 的温度用正弦波在阈值附近波动，便于触发/恢复报警；
/// - 地址 1 的湿度用余弦波，与温度错相，模拟两个独立物理量。
/// 寄存器值已放大 10 倍（对应测点 Scale=0.1），与真实设备行为一致。
/// </remarks>
public sealed class FakeDeviceClient : IDeviceClient
{
    private readonly Device _device;
    private int _tick;
    private volatile bool _isOpen;

    public FakeDeviceClient(Device device)
    {
        _device = device;
    }

    public bool IsOpen => _isOpen;
    public byte SlaveId => _device.SlaveId;

    public event Action<byte, bool>? OnHeartbeatResult;
    public event Action<byte, bool>? OnSlaveStatusChanged;

    public bool Open()
    {
        _isOpen = true;
        OnHeartbeatResult?.Invoke(_device.SlaveId, true);
        OnSlaveStatusChanged?.Invoke(_device.SlaveId, true);
        return true;
    }

    public void Close() => _isOpen = false;

    public ushort[] ReadRegisters(ModbusArea area, ushort startAddress, ushort count)
    {
        _tick++;
        var result = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            int addr = startAddress + i;
            double baseVal = addr switch
            {
                // 地址 0 = 温度：25.5 ± 3.0，波动范围 22.5 ~ 28.5，会跨过 27℃ 报警阈值
                0 => 25.5 + 3.0 * Math.Sin(_tick * 0.5),
                // 地址 1 = 湿度：50 ± 10，波动范围 40 ~ 60
                1 => 50.0 + 10.0 * Math.Cos(_tick * 0.3),
                // 其他地址给个平稳的计数值，模拟产量等
                _ => 100.0 + (_tick % 50)
            };
            result[i] = (ushort)Math.Round(baseVal * 10);
        }
        return result;
    }

    public bool[] ReadBits(ModbusArea area, ushort startAddress, ushort count)
        => Enumerable.Repeat(true, count).ToArray();

    public void WriteSingleRegister(ushort address, ushort value) { }
    public void WriteMultipleRegisters(ushort startAddress, ushort[] values) { }
    public void WriteSingleCoil(ushort address, bool value) { }
    public void WriteMultipleCoils(ushort startAddress, bool[] values) { }

    public bool ProbeOnline()
    {
        OnHeartbeatResult?.Invoke(_device.SlaveId, true);
        OnSlaveStatusChanged?.Invoke(_device.SlaveId, true);
        return true;
    }

    public void Dispose() => Close();
}
