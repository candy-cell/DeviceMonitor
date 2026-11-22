using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Demo;

/// <summary>
/// 模拟设备客户端：不碰任何真实硬件，返回预设的温湿度数据。
/// </summary>
/// <remarks>
/// 这是「IDeviceClient 抽象」带来的一大好处——测上层逻辑时，
/// 用一个假实现替换真串口，让 AcquisitionService / PollingEngine 全链路在无硬件下跑通。
/// </remarks>
public sealed class FakeDeviceClient : IDeviceClient
{
    private readonly Device _device;
    private int _tick;

    public FakeDeviceClient(Device device)
    {
        _device = device;
    }

    public bool IsOpen => true;
    public byte SlaveId => _device.SlaveId;

    public event Action<byte, bool>? OnHeartbeatResult;
    public event Action<byte, bool>? OnSlaveStatusChanged;

    public bool Open() => true;
    public void Close() { }

    public ushort[] ReadRegisters(ModbusArea area, ushort startAddress, ushort count)
    {
        _tick++;
        // 返回 count 个寄存器。地址 0 是温度（×0.1），地址 1 是湿度（×0.1）。
        // 用一个正弦波让温度在 23~28℃ 之间波动，便于触发报警。
        var result = new ushort[count];
        for (int i = 0; i < count; i++)
        {
            double baseVal = startAddress == 0
                ? 25.5 + 3.0 * Math.Sin(_tick * 0.5)   // 温度 22.5 ~ 28.5
                : 50.0 + 10.0 * Math.Cos(_tick * 0.3);  // 湿度 40 ~ 60
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

    public void Dispose() { }
}
