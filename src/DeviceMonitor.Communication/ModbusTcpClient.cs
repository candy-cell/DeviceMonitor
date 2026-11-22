using System.Net.Sockets;
using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;
using NModbus;

namespace DeviceMonitor.Communication;

/// <summary>
/// Modbus TCP 客户端（以太网）。实现 <see cref="IDeviceClient"/>。
/// </summary>
/// <remarks>
/// 与 RTU 客户端共用一个 <see cref="IDeviceClient"/> 抽象，但有两个关键差异：
/// 1. 底层用 <see cref="TcpClient"/> 而非 <see cref="System.IO.Ports.SerialPort"/>；
/// 2. Modbus TCP 无 CRC——可靠性由 TCP/IP 协议栈自己保证（序号确认、超时重传、校验和），
///    报文靠 MBAP 头的"长度"字段分帧，而不是像 RTU 那样靠 3.5 字符空闲时间。
///
/// 重连逻辑与 RTU 一致：先彻底关闭再重新连接，不复用不确定状态的旧 socket。
/// </remarks>
public sealed class ModbusTcpClient : IDeviceClient
{
    private readonly object _ioLock = new();
    private readonly Device _device;
    private readonly IModbusLogSink? _log;

    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
    private volatile bool _isOpen;
    private bool? _lastOnlineState;

    public ModbusTcpClient(Device device, IModbusLogSink? logSink = null)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _log = logSink;

        if (string.IsNullOrWhiteSpace(device.IpAddress))
        {
            throw new ArgumentException("TCP 设备的 IpAddress 不能为空。", nameof(device));
        }
        if (device.TcpPort is <= 0 or > 65535)
        {
            throw new ArgumentException($"TCP 端口 {device.TcpPort} 非法。", nameof(device));
        }
    }

    // ---------------- 可调参数（与 RTU 客户端对齐）----------------

    public int ReadTimeoutMs { get; set; } = 2000;
    public int WriteTimeoutMs { get; set; } = 2000;
    public int TransportRetries { get; set; } = 3;
    public int WaitToRetryMs { get; set; } = 250;
    public int OuterRetries { get; set; } = 2;
    public int OuterRetryIntervalMs { get; set; } = 500;
    public int ReconnectMaxAttempts { get; set; } = 5;
    public int ReconnectIntervalMs { get; set; } = 1000;

    public bool IsOpen => _isOpen;
    public byte SlaveId => _device.SlaveId;

    public event Action<byte, bool>? OnHeartbeatResult;
    public event Action<byte, bool>? OnSlaveStatusChanged;

    public bool Open()
    {
        lock (_ioLock)
        {
            if (_isOpen) return true;

            try
            {
                WriteLog(ModbusLogLevel.Info, "Connect",
                    $"正在连接 {_device.IpAddress}:{_device.TcpPort}，从站={_device.SlaveId}");

                var tcp = new TcpClient();
                // 设置超时后调用同步 Connect；不设的话默认可能长时间阻塞
                tcp.ReceiveTimeout = ReadTimeoutMs;
                tcp.SendTimeout = WriteTimeoutMs;
                tcp.Connect(_device.IpAddress!, _device.TcpPort);

                var master = new ModbusFactory().CreateMaster(tcp);
                master.Transport.Retries = TransportRetries;
                master.Transport.WaitToRetryMilliseconds = WaitToRetryMs;

                _tcpClient = tcp;
                _master = master;
                _isOpen = true;

                WriteLog(ModbusLogLevel.Info, "Connect",
                    $"{_device.IpAddress}:{_device.TcpPort} 连接成功（内层重试 {TransportRetries} 次 / {WaitToRetryMs}ms）");
                return true;
            }
            catch (SocketException ex)
            {
                WriteLog(ModbusLogLevel.Error, "Connect",
                    $"{_device.IpAddress}:{_device.TcpPort} 连接失败（Socket）。" +
                    $"请检查：IP/端口是否正确 / 设备是否在线 / 防火墙", ex);
                SafeCleanup();
                return false;
            }
            catch (Exception ex)
            {
                WriteLog(ModbusLogLevel.Error, "Connect",
                    $"{_device.IpAddress}:{_device.TcpPort} 连接发生未预期异常", ex);
                SafeCleanup();
                return false;
            }
        }
    }

    public void Close()
    {
        lock (_ioLock)
        {
            SafeCleanup();
            _isOpen = false;
            WriteLog(ModbusLogLevel.Info, "Connect", $"{_device.IpAddress}:{_device.TcpPort} 已断开");
        }
    }

    public ushort[] ReadRegisters(ModbusArea area, ushort startAddress, ushort count)
    {
        if (area is not (ModbusArea.InputRegister or ModbusArea.HoldingRegister))
        {
            throw new ArgumentException($"区 {area} 不是寄存器区，请用 ReadBits。", nameof(area));
        }

        byte fc = area == ModbusArea.HoldingRegister ? (byte)0x03 : (byte)0x04;
        string op = area == ModbusArea.HoldingRegister ? "ReadHoldingRegisters" : "ReadInputRegisters";

        return Execute(op, fc, m => area == ModbusArea.HoldingRegister
            ? m.ReadHoldingRegisters(_device.SlaveId, startAddress, count)
            : m.ReadInputRegisters(_device.SlaveId, startAddress, count));
    }

    public bool[] ReadBits(ModbusArea area, ushort startAddress, ushort count)
    {
        if (area is not (ModbusArea.Coil or ModbusArea.DiscreteInput))
        {
            throw new ArgumentException($"区 {area} 不是位区，请用 ReadRegisters。", nameof(area));
        }

        byte fc = area == ModbusArea.Coil ? (byte)0x01 : (byte)0x02;
        string op = area == ModbusArea.Coil ? "ReadCoils" : "ReadInputs";

        return Execute(op, fc, m => area == ModbusArea.Coil
            ? m.ReadCoils(_device.SlaveId, startAddress, count)
            : m.ReadInputs(_device.SlaveId, startAddress, count));
    }

    public void WriteSingleRegister(ushort address, ushort value)
    {
        Execute("WriteSingleRegister", 0x06, m =>
        {
            m.WriteSingleRegister(_device.SlaveId, address, value);
            return true;
        });
    }

    public void WriteMultipleRegisters(ushort startAddress, ushort[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Execute($"WriteMultipleRegisters(count={values.Length})", 0x10, m =>
        {
            m.WriteMultipleRegisters(_device.SlaveId, startAddress, values);
            return true;
        });
    }

    public void WriteSingleCoil(ushort address, bool value)
    {
        Execute("WriteSingleCoil", 0x05, m =>
        {
            m.WriteSingleCoil(_device.SlaveId, address, value);
            return true;
        });
    }

    public void WriteMultipleCoils(ushort startAddress, bool[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Execute($"WriteMultipleCoils(count={values.Length})", 0x0F, m =>
        {
            m.WriteMultipleCoils(_device.SlaveId, startAddress, values);
            return true;
        });
    }

    public bool ProbeOnline()
    {
        bool online;

        lock (_ioLock)
        {
            try
            {
                _ = Execute("Heartbeat", 0x03, m =>
                    m.ReadHoldingRegisters(_device.SlaveId, 0, 1), allowOuterRetry: false);
                online = true;
            }
            catch (SlaveException ex)
            {
                online = true;  // 能应答 = 在线，即使返回异常码
                byte code = (byte)ex.SlaveExceptionCode;
                WriteLog(ModbusLogLevel.Warn, "Heartbeat",
                    $"从站 {_device.SlaveId} 心跳返回异常码 0x{code:X2}，" +
                    $"但从站响应正常，判定为【在线】", ex, functionCode: 0x03);
            }
            catch (TimeoutException ex)
            {
                online = false;
                WriteLog(ModbusLogLevel.Warn, "Heartbeat",
                    $"从站 {_device.SlaveId} 心跳超时，判定为【离线】", ex, functionCode: 0x03);
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                online = false;
                WriteLog(ModbusLogLevel.Error, "Heartbeat",
                    $"从站 {_device.SlaveId} 网络异常，判定为【离线】", ex, functionCode: 0x03);
            }
            catch (Exception ex)
            {
                online = false;
                WriteLog(ModbusLogLevel.Error, "Heartbeat",
                    $"从站 {_device.SlaveId} 心跳发生未预期异常，保守判定为【离线】", ex, functionCode: 0x03);
            }
        }

        OnHeartbeatResult?.Invoke(_device.SlaveId, online);

        if (_lastOnlineState != online)
        {
            _lastOnlineState = online;
            OnSlaveStatusChanged?.Invoke(_device.SlaveId, online);
        }

        return online;
    }

    public void Dispose() => Close();

    // ==================== 内部实现 ====================

    private T Execute<T>(string operation, byte functionCode, Func<IModbusMaster, T> action,
        bool allowOuterRetry = true)
    {
        lock (_ioLock)
        {
            int attempts = allowOuterRetry ? OuterRetries + 1 : 1;

            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                try
                {
                    if (!EnsureConnected())
                    {
                        throw new IOException($"从站 {_device.SlaveId} 连接不可用，重连失败。");
                    }
                    return action(_master!);
                }
                catch (SlaveException)
                {
                    throw;  // 从站明确应答，重试无意义
                }
                catch (TimeoutException ex)
                {
                    if (attempt >= attempts)
                    {
                        WriteLog(ModbusLogLevel.Error, operation,
                            $"从站 {_device.SlaveId} {operation} 超时，已重试 {attempts - 1} 次仍未成功",
                            ex, functionCode: functionCode);
                        throw;
                    }
                    WriteLog(ModbusLogLevel.Warn, operation,
                        $"从站 {_device.SlaveId} {operation} 超时，第 {attempt} 次重试", ex, functionCode: functionCode);
                    Thread.Sleep(OuterRetryIntervalMs);
                }
                catch (Exception ex) when (ex is IOException or SocketException)
                {
                    WriteLog(ModbusLogLevel.Warn, operation,
                        $"从站 {_device.SlaveId} {operation} 网络异常，先重连再重试", ex, functionCode: functionCode);
                    ForceReconnect();
                    if (attempt >= attempts) throw;
                    Thread.Sleep(OuterRetryIntervalMs);
                }
            }

            throw new InvalidOperationException($"{operation} 执行失败。");
        }
    }

    private bool EnsureConnected()
    {
        if (_isOpen && _tcpClient?.Connected == true && _master is not null)
        {
            return true;
        }

        WriteLog(ModbusLogLevel.Warn, "Reconnect", $"从站 {_device.SlaveId} 连接已断开，开始重连");

        for (int attempt = 1; attempt <= ReconnectMaxAttempts; attempt++)
        {
            SafeCleanup();

            try
            {
                var tcp = new TcpClient
                {
                    ReceiveTimeout = ReadTimeoutMs,
                    SendTimeout = WriteTimeoutMs
                };
                tcp.Connect(_device.IpAddress!, _device.TcpPort);

                var master = new ModbusFactory().CreateMaster(tcp);
                master.Transport.Retries = TransportRetries;
                master.Transport.WaitToRetryMilliseconds = WaitToRetryMs;

                _tcpClient = tcp;
                _master = master;
                _isOpen = true;

                WriteLog(ModbusLogLevel.Info, "Reconnect",
                    $"从站 {_device.SlaveId} 第 {attempt} 次重连成功");
                return true;
            }
            catch (Exception ex)
            {
                WriteLog(ModbusLogLevel.Warn, "Reconnect",
                    $"从站 {_device.SlaveId} 第 {attempt}/{ReconnectMaxAttempts} 次重连失败", ex);
                if (attempt < ReconnectMaxAttempts) Thread.Sleep(ReconnectIntervalMs);
            }
        }

        WriteLog(ModbusLogLevel.Error, "Reconnect",
            $"从站 {_device.SlaveId} 重连 {ReconnectMaxAttempts} 次全部失败，放弃本次通信");
        return false;
    }

    private void ForceReconnect()
    {
        _isOpen = false;
        SafeCleanup();
        EnsureConnected();
    }

    private void SafeCleanup()
    {
        try { _master?.Dispose(); } catch { /* ignore */ }
        _master = null;
        try { _tcpClient?.Close(); } catch { /* ignore */ }
        _tcpClient = null;
        _isOpen = false;
    }

    private void WriteLog(ModbusLogLevel level, string category, string message,
        Exception? ex = null, byte? functionCode = null)
    {
        _log?.Write(new ModbusLogEntry
        {
            Level = level,
            Category = category,
            Message = message,
            Exception = ex,
            SlaveId = _device.SlaveId,
            FunctionCode = functionCode
        });
    }
}
