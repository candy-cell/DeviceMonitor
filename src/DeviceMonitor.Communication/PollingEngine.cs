using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Communication;

/// <summary>
/// 一次轮询读到的原始数据。
/// </summary>
/// <param name="PointId">测点 Id。</param>
/// <param name="RawValues">寄存器原始值（已按测点读出的 16 位值，可能 1 个或 2 个）。</param>
/// <param name="Bits">位值（仅线圈/离散输入区使用；寄存器区为 null）。</param>
public readonly record struct PollResult(int PointId, ushort[] RawValues, bool[]? Bits);

/// <summary>
/// 设备连接状态变化事件参数。
/// </summary>
/// <param name="DeviceId">设备 Id。</param>
/// <param name="DeviceName">设备名称。</param>
/// <param name="Online">是否在线。</param>
/// <param name="Reason">状态变化原因（如"响应超时"、"串口 IO 异常"）。</param>
public readonly record struct DeviceStatusChanged(int DeviceId, string DeviceName, bool Online, string Reason);

/// <summary>
/// 轮询引擎：驱动"谁在不停地读数据"，同时检测设备在线状态。
/// </summary>
/// <remarks>
/// 三条铁律（来自 RS485 半双工总线特性 + 教材 §7.4）：
/// <list type="number">
/// <item><b>顺序执行，不并发</b>——单线程遍历，一次只跟一个从站通信。</item>
/// <item><b>从站间 50ms 静默</b>——给从站释放总线的时间，否则紧接着发下一帧会冲突。</item>
/// <item><b>单点容错</b>——某个测点/设备失败，记日志继续下一个，绝不中断整轮。</item>
/// </list>
///
/// 引擎只管"读"，不碰任何 UI。结果通过回调交给上层（上层再决定是存库还是画曲线）。
/// 设备在线状态通过 <see cref="DeviceStatusChanged"/> 事件抛出（只在状态跃变时触发，不刷屏）。
/// </remarks>
public sealed class PollingEngine : IDisposable
{
    private const int InterItemDelayMs = 50;

    private readonly IDeviceClientFactory _clientFactory;
    private readonly IModbusLogSink? _log;

    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private readonly object _lifecycleLock = new();
    private bool _disposed;

    public PollingEngine(IDeviceClientFactory clientFactory, IModbusLogSink? logSink = null)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _log = logSink;
    }

    /// <summary>完成一整轮后、进入下一轮前的等待（毫秒）。</summary>
    public int CycleIntervalMs { get; set; } = 1000;

    /// <summary>引擎是否正在运行。</summary>
    public bool IsRunning => _cts is { IsCancellationRequested: false };

    /// <summary>
    /// 设备在线状态跃变时触发（离线或恢复都触发，只触发一次，不刷屏）。
    /// </summary>
    public event Action<DeviceStatusChanged>? DeviceStatusChanged;

    public void Start(IEnumerable<Device> devices, Action<PollResult>? onData)
    {
        ArgumentNullException.ThrowIfNull(devices);

        var items = Flatten(devices).ToList();
        if (items.Count == 0)
        {
            WriteLog(ModbusLogLevel.Warn, "Poll", "轮询列表为空，未启动");
            return;
        }

        Stop();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        WriteLog(ModbusLogLevel.Info, "Poll",
            $"轮询启动：设备 {devices.Count()} 个，测点 {items.Count} 个，周期 {CycleIntervalMs}ms");

        _pollingTask = Task.Run(() => RunLoop(items, onData, token), token);
    }

    public void Stop()
    {
        lock (_lifecycleLock)
        {
            if (_cts is null && _pollingTask is null) return;

            try
            {
                _cts?.Cancel();
                _pollingTask?.Wait(TimeSpan.FromSeconds(3));
            }
            catch (AggregateException) { }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                _pollingTask = null;
            }
        }

        WriteLog(ModbusLogLevel.Info, "Poll", "轮询已停止");
    }

    // ==================== 内部实现 ====================

    private void RunLoop(List<PollItem> items, Action<PollResult>? onData, CancellationToken token)
    {
        // 每台设备一个客户端实例 + 一个在线状态
        var clients = new Dictionary<int, IDeviceClient>();
        var onlineState = new Dictionary<int, bool>();   // 最近一次已知的在线状态
        var deviceNames = new Dictionary<int, string>();

        try
        {
            foreach (var group in items.GroupBy(i => i.Device.Id))
            {
                var device = group.First().Device;
                deviceNames[device.Id] = device.Name;

                try
                {
                    var client = _clientFactory.Create(device, _log);
                    if (client.Open())
                    {
                        clients[device.Id] = client;
                        onlineState[device.Id] = true;
                    }
                    else
                    {
                        onlineState[device.Id] = false;
                        WriteLog(ModbusLogLevel.Error, "Poll",
                            $"设备 {device.Name} 连接失败，本轮跳过");
                        DeviceStatusChanged?.Invoke(new DeviceStatusChanged(
                            device.Id, device.Name, false, "连接失败"));
                    }
                }
                catch (Exception ex)
                {
                    onlineState[device.Id] = false;
                    WriteLog(ModbusLogLevel.Error, "Poll", $"设备 {device.Name} 创建客户端失败", ex);
                    DeviceStatusChanged?.Invoke(new DeviceStatusChanged(
                        device.Id, device.Name, false, ex.Message));
                }
            }

            while (!token.IsCancellationRequested)
            {
                // 按设备分组，一次处理一个设备的所有测点，便于判断"设备级"在线状态
                foreach (var deviceGroup in items.GroupBy(i => i.Device.Id))
                {
                    if (token.IsCancellationRequested) break;

                    int deviceId = deviceGroup.Key;
                    string deviceName = deviceNames[deviceId];

                    if (!clients.TryGetValue(deviceId, out var client))
                    {
                        // 客户端没建成功，保持离线，继续下一台
                        continue;
                    }

                    bool anySuccess = false;
                    string? failReason = null;

                    foreach (var item in deviceGroup)
                    {
                        if (token.IsCancellationRequested) break;

                        try
                        {
                            var result = ReadOne(client, item);
                            onData?.Invoke(result);
                            anySuccess = true;
                        }
                        catch (TimeoutException ex)
                        {
                            failReason = "响应超时";
                            WriteLog(ModbusLogLevel.Warn, "Poll",
                                $"设备 {deviceName} 测点 {item.DataPoint.Name} 超时", ex);
                        }
                        catch (IOException ex)
                        {
                            failReason = "串口/网络 IO 异常";
                            WriteLog(ModbusLogLevel.Error, "Poll",
                                $"设备 {deviceName} 测点 {item.DataPoint.Name} IO 异常", ex);
                        }
                        catch (Exception ex)
                        {
                            failReason = ex.GetType().Name;
                            WriteLog(ModbusLogLevel.Error, "Poll",
                                $"设备 {deviceName} 测点 {item.DataPoint.Name} 轮询失败", ex);
                        }

                        try { Task.Delay(InterItemDelayMs, token).Wait(token); }
                        catch (OperationCanceledException) { break; }
                    }

                    // 判定设备在线状态：本设备至少一个测点成功 → 在线；全失败 → 离线
                    bool online = anySuccess;

                    if (onlineState.TryGetValue(deviceId, out var prev) && prev != online)
                    {
                        // 状态跃变，抛事件
                        onlineState[deviceId] = online;
                        DeviceStatusChanged?.Invoke(new DeviceStatusChanged(
                            deviceId, deviceName, online,
                            online ? "恢复正常" : (failReason ?? "未知原因")));
                    }
                    else if (!onlineState.ContainsKey(deviceId))
                    {
                        onlineState[deviceId] = online;
                    }
                }

                try { Task.Delay(CycleIntervalMs, token).Wait(token); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            foreach (var c in clients.Values)
            {
                try { c.Dispose(); } catch { /* ignore */ }
            }
            clients.Clear();
        }

        WriteLog(ModbusLogLevel.Info, "Poll", "轮询循环已退出");
    }

    private static PollResult ReadOne(IDeviceClient client, PollItem item)
    {
        var p = item.DataPoint;
        if (p.IsBitType)
        {
            var bits = client.ReadBits(p.Area, p.Address, 1);
            return new PollResult(p.Id, Array.Empty<ushort>(), bits);
        }
        else
        {
            var regs = client.ReadRegisters(p.Area, p.Address, p.RegisterCount);
            return new PollResult(p.Id, regs, null);
        }
    }

    private static IEnumerable<PollItem> Flatten(IEnumerable<Device> devices)
    {
        foreach (var device in devices)
        {
            foreach (var point in device.DataPoints)
            {
                if (!point.Enabled) continue;
                yield return new PollItem(device, point);
            }
        }
    }

    private readonly record struct PollItem(Device Device, DataPoint DataPoint);

    private void WriteLog(ModbusLogLevel level, string category, string message, Exception? ex = null)
    {
        _log?.Write(new ModbusLogEntry
        {
            Level = level,
            Category = category,
            Message = message,
            Exception = ex
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
