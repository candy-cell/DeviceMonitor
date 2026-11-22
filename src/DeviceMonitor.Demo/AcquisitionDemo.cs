using DeviceMonitor.Application;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;
using DeviceMonitor.Storage;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Demo;

/// <summary>
/// 采集服务端到端验证：用假设备客户端，跑通「轮询 → 换算 → 落库 → 报警」全链路，无硬件。
/// </summary>
internal static class AcquisitionDemo
{
    public static async Task RunAsync(string dbPath)
    {
        Console.WriteLine("\n========== 采集服务端到端演示 ==========\n");

        await using var db = MonitorDbContextFactory.Create(dbPath);
        await db.Database.EnsureCreatedAsync();

        // 建一个设备 + 一个温度测点 + 一条超上限规则
        var device = new Device
        {
            Name = "模拟设备",
            Protocol = DeviceProtocol.Rtu,
            SlaveId = 1,
            PortName = "FAKE",
            Enabled = true
        };
        var tempPoint = new DataPoint
        {
            Name = "温度",
            Area = ModbusArea.InputRegister,
            Address = 0,
            DataType = DataValueType.UInt16,
            Scale = 0.1,
            Unit = "℃",
            DecimalPlaces = 1
        };
        device.DataPoints.Add(tempPoint);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var rule = new AlarmRule
        {
            DataPointId = tempPoint.Id,
            Type = AlarmType.High,
            Threshold = 27.0,
            Deadband = 0.5,
            DelayMs = 1500,
            Level = AlarmLevel.Warning,
            Enabled = true,
            Remark = "温度超上限"
        };
        db.AlarmRules.Add(rule);
        await db.SaveChangesAsync();

        // 组装采集服务（用假工厂，无硬件）
        // 仓储依赖 IDbContextFactory（每个方法独立 DbContext，线程安全）
        var dbFactory = new MonitorDbContextFactory(dbPath);
        var deviceRepo = new DeviceRepository(dbFactory);
        var sampleRepo = new SampleRepository(dbFactory);
        var alarmRepo = new AlarmRepository(dbFactory);
        var service = new AcquisitionService(
            new FakeDeviceClientFactory(),
            deviceRepo, sampleRepo, alarmRepo,
            sampleBatchSize: 5, sampleBatchInterval: TimeSpan.FromMilliseconds(500));

        int received = 0;
        int alarms = 0;
        service.DataReceived += (p, v, t) =>
        {
            received++;
            if (received <= 8)
                Console.WriteLine($"  📊 {t:HH:mm:ss}  {p.Name}={v:F1}{p.Unit}");
        };
        service.AlarmChanged += r =>
        {
            alarms++;
            Console.WriteLine($"  🚨 报警状态变化：{r.State}  {r.Message}");
        };

        // 跑 5 秒，期间温度在 22.5~28.5 波动，会跨过 27℃ 阈值触发报警
        await service.StartAsync();
        Console.WriteLine("采集已启动，运行 5 秒...\n");
        await Task.Delay(5000);
        await service.StopAsync();

        // 检查结果
        var sampleCount = await db.Samples.CountAsync();
        var alarmCount = await db.AlarmRecords.CountAsync();
        Console.WriteLine($"\n  累计收到数据 {received} 次，报警事件 {alarms} 次");
        Console.WriteLine($"  落库采样 {sampleCount} 条，报警记录 {alarmCount} 条");

        Console.WriteLine("\n========== 采集服务演示结束 ==========\n");
    }
}
