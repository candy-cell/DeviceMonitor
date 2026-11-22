using DeviceMonitor.Alarms;
using DeviceMonitor.Communication;
using DeviceMonitor.Core;
using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Application;

/// <summary>
/// 采集服务：把「轮询引擎」读到的原始数据，串成「换算 → 落库 → 报警」的完整链路。
/// </summary>
/// <remarks>
/// 这是粘合层，负责把 Core / Communication / Storage / Alarms 四个模块捏成一个系统。
/// 它本身不碰硬件（由轮询引擎代理）、不碰数据库细节（由仓储代理）、
/// 不碰 UI（通过事件抛出）。
///
/// 数据流：
/// <code>
/// PollingEngine.ReadOne  →  PollResult(PointId, RawValues, Bits)
///        │
///        ├─ ValueInterpreter → 工程值（×Scale + Offset）
///        │
///        ├─ Sample 落库（ISampleRepository，批量攒批）
///        │
///        └─ AlarmEngine.Evaluate → 报警状态机 → 事件 → 报警落库
/// </code>
/// </remarks>
public sealed class AcquisitionService : IDisposable
{
    private readonly PollingEngine _engine;
    private readonly IDeviceClientFactory _clientFactory;
    private readonly IDeviceRepository _deviceRepo;
    private readonly ISampleRepository _sampleRepo;
    private readonly IAlarmRepository _alarmRepo;
    private readonly AlarmEngine _alarmEngine;

    // 攒批：采样先攒着，够一批或够时间才落库，避免一条一 INSERT
    private readonly List<Sample> _pendingSamples = new();
    private readonly object _batchLock = new();
    private readonly int _batchSize;
    private readonly TimeSpan _batchInterval;

    private Dictionary<int, DataPoint> _pointMap = new();
    private bool _started;

    /// <summary>每个测点采到一次数据就触发（参数：测点、工程值、时刻）。供 UI 刷新。</summary>
    public event Action<DataPoint, double, DateTime>? DataReceived;

    /// <summary>报警状态变化时触发（参数：报警记录）。供 UI 弹窗/变色。</summary>
    public event Action<AlarmRecord>? AlarmChanged;

    /// <summary>设备在线状态变化时触发（参数：设备连接状态）。供 UI 提示。</summary>
    public event Action<DeviceStatusChanged>? DeviceStatusChanged;

    public AcquisitionService(
        IDeviceClientFactory clientFactory,
        IDeviceRepository deviceRepo,
        ISampleRepository sampleRepo,
        IAlarmRepository alarmRepo,
        AlarmEngine? alarmEngine = null,
        int sampleBatchSize = 100,
        TimeSpan? sampleBatchInterval = null)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _deviceRepo = deviceRepo ?? throw new ArgumentNullException(nameof(deviceRepo));
        _sampleRepo = sampleRepo ?? throw new ArgumentNullException(nameof(sampleRepo));
        _alarmRepo = alarmRepo ?? throw new ArgumentNullException(nameof(alarmRepo));

        _alarmEngine = alarmEngine ?? new AlarmEngine();
        _engine = new PollingEngine(clientFactory, null);
        _batchSize = sampleBatchSize;
        _batchInterval = sampleBatchInterval ?? TimeSpan.FromSeconds(1);

        // 设备在线状态变化 → 转发给上层
        _engine.DeviceStatusChanged += s => DeviceStatusChanged?.Invoke(s);

        // 报警引擎事件 → 落库 + 转发给上层
        _alarmEngine.Triggered += async r => { await PersistAlarm(r); AlarmChanged?.Invoke(r); };
        _alarmEngine.Acknowledged += async r => { await _alarmRepo.UpdateRecordAsync(r); AlarmChanged?.Invoke(r); };
        _alarmEngine.Cleared += async r => { await _alarmRepo.UpdateRecordAsync(r); AlarmChanged?.Invoke(r); };
    }

    /// <summary>
    /// 启动采集。会加载全部启用设备与测点，装载报警规则，然后启动轮询。
    /// </summary>
    public async Task StartAsync()
    {
        if (_started) return;

        // 1) 加载设备 + 测点，建立 PointId → DataPoint 映射
        var devices = await _deviceRepo.GetEnabledAsync();
        _pointMap = devices
            .SelectMany(d => d.DataPoints)
            .Where(p => p.Enabled)
            .ToDictionary(p => p.Id);

        // 2) 装载报警规则，喂给报警引擎（引擎会在 Evaluate 时按 rule.Id 找状态）
        var rules = await _alarmRepo.GetEnabledRulesAsync();
        // 规则由引擎在 Evaluate 时惰性创建状态，这里无需预注册，只需保存引用供查询
        _rules = rules;

        // 3) 启动轮询
        _engine.Start(devices, OnData);
        _started = true;
    }

    private List<AlarmRule> _rules = new();

    /// <summary>停止采集，并冲刷剩余未落库的采样。</summary>
    public async Task StopAsync()
    {
        if (!_started) return;
        _engine.Stop();
        await FlushSamplesAsync();
        _started = false;
    }

    // ==================== 轮询回调 ====================

    private void OnData(PollResult result)
    {
        if (!_pointMap.TryGetValue(result.PointId, out var point))
        {
            return;  // 测点已删除或禁用，忽略
        }

        double value;
        try
        {
            value = ValueInterpreter.ToEngineering(point, result.RawValues, result.Bits);
        }
        catch (Exception)
        {
            // 数据不足（如 32 位类型只读到 1 个寄存器），跳过本次，不中断采集
            return;
        }

        var now = DateTime.Now;

        // 1) 生成 Sample 攒批
        var sample = new Sample
        {
            DataPointId = point.Id,
            Timestamp = now,
            Value = value,
            Quality = QualityFlag.Good
        };
        EnqueueSample(sample);

        // 2) 喂报警引擎
        foreach (var rule in _rules.Where(r => r.DataPointId == point.Id))
        {
            _alarmEngine.Evaluate(rule, value, now);
        }

        // 3) 通知 UI
        DataReceived?.Invoke(point, value, now);
    }

    // ==================== 采样攒批 ====================

    private void EnqueueSample(Sample sample)
    {
        lock (_batchLock)
        {
            _pendingSamples.Add(sample);
            if (_pendingSamples.Count >= _batchSize)
            {
                FlushPendingSamples();
            }
        }
    }

    private void FlushPendingSamples()
    {
        if (_pendingSamples.Count == 0) return;

        var batch = _pendingSamples.ToArray();
        _pendingSamples.Clear();

        // 落库是异步的，这里 fire-and-forget 但保留异常日志
        _ = Task.Run(async () =>
        {
            try
            {
                await _sampleRepo.AddRangeAsync(batch);
            }
            catch (Exception)
            {
                // 采样落库失败不应崩溃采集循环，只记日志（正式版接日志）
            }
        });
    }

    private async Task FlushSamplesAsync()
    {
        lock (_batchLock)
        {
            FlushPendingSamples();
        }
        // 等待最后一个落库任务结束（简化处理）
        await Task.Delay(200);
    }

    // ==================== 报警落库 ====================

    private async Task PersistAlarm(AlarmRecord record)
    {
        try
        {
            await _alarmRepo.AddRecordAsync(record);
        }
        catch (Exception)
        {
            // 报警落库失败不影响报警状态机继续跑
        }
    }

    public void Dispose()
    {
        _engine.Dispose();
        _ = FlushSamplesAsync();
    }
}
