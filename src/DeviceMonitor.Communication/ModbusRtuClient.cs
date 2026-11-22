using System.IO.Ports;
using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;
using NModbus;
using NModbus.Serial;

namespace DeviceMonitor.Communication;

/// <summary>
/// Modbus RTU 客户端（串口）。实现 <see cref="IDeviceClient"/>，是"六件套"的落地：
/// CRC 校验 → 由 NModbus 传输层负责；
/// 掉线重连 → <see cref="EnsureConnected"/>；
/// 重试 → 异常分类 + 双层重试；
/// 心跳 → <see cref="ProbeOnline"/>（异常码也算在线）；
/// 结构化日志 → <see cref="IModbusLogSink"/>。
/// </summary>
/// <remarks>
/// 所有串口访问都串行化在一把锁里。原因：RS485 是半双工总线，
/// 同一时刻只能有一方说话，并发访问必然导致帧冲突。
/// </remarks>
public sealed class ModbusRtuClient : IDeviceClient
{
    /// <summary>从站返回"非法数据地址"异常码。</summary>
    private const byte IllegalDataAddress = 0x02;

    private readonly object _ioLock = new();
    private readonly Device _device;
    private readonly IModbusLogSink? _log;

    /// <summary>已完成非空校验的串口名，避免到处写 <c>_portName!</c>。</summary>
    private readonly string _portName;

    private SerialPort? _serialPort;
    private IModbusMaster? _master;
    private volatile bool _isOpen;

    /// <summary>上一次的在线状态，用于判断"状态跃变"。</summary>
    private bool? _lastOnlineState;

    public ModbusRtuClient(Device device, IModbusLogSink? logSink = null)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _log = logSink;

        if (string.IsNullOrWhiteSpace(device.PortName))
        {
            throw new ArgumentException("RTU 设备的 PortName 不能为空。", nameof(device));
        }

        // 在这里定死非空，后续就只用 _portName
        _portName = device.PortName;
    }

    // ---------------- 可调参数（全部有合理默认值）----------------

    /// <summary>读超时（毫秒）。</summary>
    public int ReadTimeoutMs { get; set; } = 2000;

    /// <summary>写超时（毫秒）。</summary>
    public int WriteTimeoutMs { get; set; } = 2000;

    /// <summary>NModbus 传输层重试次数（内层，毫秒级，对抗瞬时干扰）。</summary>
    public int TransportRetries { get; set; } = 3;

    /// <summary>NModbus 传输层每次重试间隔（毫秒）。</summary>
    public int WaitToRetryMs { get; set; } = 250;

    /// <summary>外层重试次数（数百毫秒级，等从站从繁忙恢复）。仅对超时类生效。</summary>
    public int OuterRetries { get; set; } = 2;

    /// <summary>外层每次重试前等待（毫秒）。</summary>
    public int OuterRetryIntervalMs { get; set; } = 500;

    /// <summary>掉线重连最大尝试次数。</summary>
    public int ReconnectMaxAttempts { get; set; } = 5;

    /// <summary>每次重连尝试之间的等待（毫秒）。</summary>
    public int ReconnectIntervalMs { get; set; } = 1000;

    // ---------------- IDeviceClient ----------------

    /// <inheritdoc />
    public bool IsOpen => _isOpen;

    /// <inheritdoc />
    public byte SlaveId => _device.SlaveId;

    /// <inheritdoc />
    public event Action<byte, bool>? OnHeartbeatResult;

    /// <inheritdoc />
    public event Action<byte, bool>? OnSlaveStatusChanged;

    /// <inheritdoc />
    public bool Open()
    {
        lock (_ioLock)
        {
            if (_isOpen) return true;

            try
            {
                WriteLog(ModbusLogLevel.Info, "Connect",
                    $"正在打开串口 {_portName}（{_device.BaudRate},{_device.DataBits}," +
                    $"{DescribeParity(_device.Parity)},{_device.StopBits}）从站={_device.SlaveId}");

                // 1) 物理串口
                var sp = new SerialPort(_portName)
                {
                    BaudRate = _device.BaudRate,
                    DataBits = _device.DataBits,
                    StopBits = ToStopBits(_device.StopBits),
                    Parity = ToParity(_device.Parity),
                    ReadTimeout = ReadTimeoutMs,
                    WriteTimeout = WriteTimeoutMs
                };
                sp.Open();

                // 2) 用 NModbus 工厂创建 RTU 主站
                var factory = new ModbusFactory();
                var master = factory.CreateRtuMaster(new SerialPortAdapter(sp));

                // 3) 传输层参数（内层重试）
                master.Transport.ReadTimeout = ReadTimeoutMs;
                master.Transport.WriteTimeout = WriteTimeoutMs;
                master.Transport.Retries = TransportRetries;
                master.Transport.WaitToRetryMilliseconds = WaitToRetryMs;

                _serialPort = sp;
                _master = master;
                _isOpen = true;

                WriteLog(ModbusLogLevel.Info, "Connect",
                    $"串口 {_portName} 打开成功（内层重试 {TransportRetries} 次 / {WaitToRetryMs}ms）");
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                WriteLog(ModbusLogLevel.Error, "Connect",
                    $"串口 {_portName} 被占用或无权限。请检查：是否被其他程序占用 / 是否有访问权限", ex);
                SafeCleanup();
                return false;
            }
            catch (IOException ex)
            {
                WriteLog(ModbusLogLevel.Error, "Connect",
                    $"串口 {_portName} 打开失败（IO）。请检查：串口号 / USB 转串口驱动 / 线缆", ex);
                SafeCleanup();
                return false;
            }
            catch (ArgumentException ex)
            {
                WriteLog(ModbusLogLevel.Error, "Connect",
                    $"串口参数无效：PortName={_portName}, BaudRate={_device.BaudRate}", ex);
                SafeCleanup();
                return false;
            }
            catch (Exception ex)
            {
                WriteLog(ModbusLogLevel.Error, "Connect",
                    $"打开串口 {_portName} 发生未预期异常", ex);
                SafeCleanup();
                return false;
            }
        }
    }

    /// <inheritdoc />
    public void Close()
    {
        lock (_ioLock)
        {
            SafeCleanup();
            _isOpen = false;
            WriteLog(ModbusLogLevel.Info, "Connect", $"串口 {_portName} 已关闭");
        }
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void WriteSingleRegister(ushort address, ushort value)
    {
        Execute("WriteSingleRegister", 0x06, m =>
        {
            m.WriteSingleRegister(_device.SlaveId, address, value);
            return true;
        });
    }

    /// <inheritdoc />
    public void WriteMultipleRegisters(ushort startAddress, ushort[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Execute($"WriteMultipleRegisters(count={values.Length})", 0x10, m =>
        {
            m.WriteMultipleRegisters(_device.SlaveId, startAddress, values);
            return true;
        });
    }

    /// <inheritdoc />
    public void WriteSingleCoil(ushort address, bool value)
    {
        Execute("WriteSingleCoil", 0x05, m =>
        {
            m.WriteSingleCoil(_device.SlaveId, address, value);
            return true;
        });
    }

    /// <inheritdoc />
    public void WriteMultipleCoils(ushort startAddress, bool[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Execute($"WriteMultipleCoils(count={values.Length})", 0x0F, m =>
        {
            m.WriteMultipleCoils(_device.SlaveId, startAddress, values);
            return true;
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// 判定规则（这是本类的关键设计）：
    /// 正常返回   → 在线；<br/>
    /// 返回异常码 → <b>仍然在线</b>（能应答就说明它活着，只是地址/功能码配置有问题）；<br/>
    /// 超时 / IO 异常 / 其他 → 离线。
    /// </remarks>
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
                // ★ 从站响应了异常码 → 它活着 → 判在线
                online = true;
                byte code = (byte)ex.SlaveExceptionCode;

                if (code == IllegalDataAddress)
                {
                    WriteLog(ModbusLogLevel.Warn, "Heartbeat",
                        $"【存储区域有误】从站 {_device.SlaveId} 心跳地址 0x0000 不存在或不可读，" +
                        $"但从站响应正常，判定为【在线】。请核对从站保持寄存器起始地址配置",
                        ex, functionCode: 0x03);
                }
                else
                {
                    WriteLog(ModbusLogLevel.Warn, "Heartbeat",
                        $"从站 {_device.SlaveId} 心跳返回异常码 0x{code:X2}，" +
                        $"但从站响应正常，判定为【在线】",
                        ex, functionCode: 0x03);
                }
            }
            catch (TimeoutException ex)
            {
                online = false;
                WriteLog(ModbusLogLevel.Warn, "Heartbeat",
                    $"从站 {_device.SlaveId} 心跳超时（{ReadTimeoutMs}ms），判定为【离线】", ex, functionCode: 0x03);
            }
            catch (IOException ex)
            {
                online = false;
                WriteLog(ModbusLogLevel.Error, "Heartbeat",
                    $"从站 {_device.SlaveId} 心跳 IO 异常，判定为【离线】", ex, functionCode: 0x03);
            }
            catch (Exception ex)
            {
                online = false;
                WriteLog(ModbusLogLevel.Error, "Heartbeat",
                    $"从站 {_device.SlaveId} 心跳发生未预期异常，保守判定为【离线】", ex, functionCode: 0x03);
            }
        }

        // 每次心跳都通知（适合 UI 实时刷新）
        OnHeartbeatResult?.Invoke(_device.SlaveId, online);

        // 仅状态跃变时通知（避免日志刷屏）
        if (_lastOnlineState != online)
        {
            _lastOnlineState = online;
            OnSlaveStatusChanged?.Invoke(_device.SlaveId, online);
        }

        return online;
    }

    /// <inheritdoc />
    public void Dispose() => Close();

    // ==================== 内部实现 ====================

    /// <summary>
    /// 统一的执行骨架：确保连接 → 执行 → 异常分类处理。
    /// </summary>
    /// <remarks>
    /// 异常分类策略：
    /// <list type="bullet">
    /// <item><see cref="SlaveException"/>：从站已明确应答，<b>不重试</b>，直接上抛。</item>
    /// <item><see cref="TimeoutException"/>：从站没应答，可能是瞬时干扰或忙 → 外层重试。</item>
    /// <item><see cref="IOException"/>：物理层断了 → 强制重连后再试。</item>
    /// </list>
    /// </remarks>
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
                    // 从站明确应答，重试无意义
                    throw;
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
                        $"从站 {_device.SlaveId} {operation} 超时，第 {attempt} 次重试（等待 {OuterRetryIntervalMs}ms）",
                        ex, functionCode: functionCode);
                    Thread.Sleep(OuterRetryIntervalMs);
                }
                catch (IOException ex)
                {
                    WriteLog(ModbusLogLevel.Warn, operation,
                        $"从站 {_device.SlaveId} {operation} 出现 IO 异常，先重连再重试", ex, functionCode: functionCode);

                    ForceReconnect();

                    if (attempt >= attempts)
                    {
                        throw;
                    }
                    Thread.Sleep(OuterRetryIntervalMs);
                }
            }

            // 理论上不可达
            throw new InvalidOperationException($"{operation} 执行失败。");
        }
    }

    /// <summary>
    /// 确保连接可用：断了就主动重连，而不是等异常再补救。
    /// </summary>
    /// <remarks>
    /// 重连采用"先彻底关闭再重新打开"，因为断开后的串口对象往往处于不确定状态，
    /// 直接复用可能失败或行为异常，重新创建更干净。
    /// </remarks>
    private bool EnsureConnected()
    {
        if (_isOpen && _serialPort?.IsOpen == true && _master is not null)
        {
            return true;
        }

        WriteLog(ModbusLogLevel.Warn, "Reconnect", $"从站 {_device.SlaveId} 连接已断开，开始重连");

        for (int attempt = 1; attempt <= ReconnectMaxAttempts; attempt++)
        {
            SafeCleanup();

            try
            {
                var sp = new SerialPort(_portName)
                {
                    BaudRate = _device.BaudRate,
                    DataBits = _device.DataBits,
                    StopBits = ToStopBits(_device.StopBits),
                    Parity = ToParity(_device.Parity),
                    ReadTimeout = ReadTimeoutMs,
                    WriteTimeout = WriteTimeoutMs
                };
                sp.Open();

                var master = new ModbusFactory().CreateRtuMaster(new SerialPortAdapter(sp));
                master.Transport.ReadTimeout = ReadTimeoutMs;
                master.Transport.WriteTimeout = WriteTimeoutMs;
                master.Transport.Retries = TransportRetries;
                master.Transport.WaitToRetryMilliseconds = WaitToRetryMs;

                _serialPort = sp;
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

                if (attempt < ReconnectMaxAttempts)
                {
                    Thread.Sleep(ReconnectIntervalMs);
                }
            }
        }

        WriteLog(ModbusLogLevel.Error, "Reconnect",
            $"从站 {_device.SlaveId} 重连 {ReconnectMaxAttempts} 次全部失败，放弃本次通信");
        return false;
    }

    /// <summary>IO 异常后强制重建连接。</summary>
    private void ForceReconnect()
    {
        _isOpen = false;
        SafeCleanup();
        EnsureConnected();
    }

    /// <summary>安全清理串口与主站对象，异常一律吞掉（清理阶段不应再抛）。</summary>
    private void SafeCleanup()
    {
        try { _master?.Dispose(); } catch { /* ignore */ }
        _master = null;

        try
        {
            if (_serialPort?.IsOpen == true) _serialPort.Close();
        }
        catch { /* ignore */ }

        try { _serialPort?.Dispose(); } catch { /* ignore */ }
        _serialPort = null;

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

    private static StopBits ToStopBits(int v) => v switch
    {
        2 => StopBits.Two,
        15 => StopBits.OnePointFive,
        _ => StopBits.One
    };

    private static Parity ToParity(int v) => v switch
    {
        1 => Parity.Odd,
        2 => Parity.Even,
        3 => Parity.Mark,
        4 => Parity.Space,
        _ => Parity.None
    };

    private static string DescribeParity(int v) => ToParity(v).ToString();
}
