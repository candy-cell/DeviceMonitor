using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;
using DeviceMonitor.Storage;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Demo;

/// <summary>
/// 存储层验证 Demo：建库 → 插设备/测点/采样 → 查询 → 聚合 → 打印。
/// </summary>
/// <remarks>
/// 这一步不碰任何硬件，只验证 EF Core + SQLite 整条链路能跑通。
/// 运行后会在当前目录生成 MonitorDemo.db，可以用 Letos（原 SQLiteStudio）打开查看表结构。
/// </remarks>
internal static class Program
{
    private static async Task Main()
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, $"MonitorDemo_{DateTime.Now:HHmmss}.db");
        // 不再删旧库——SQLite 句柄可能被上一次运行残留占用，直接每次用新文件名最稳

        Console.WriteLine($"数据库路径：{dbPath}\n");

        await using var db = MonitorDbContextFactory.Create(dbPath);

        // 1) 建库（EnsureCreated 等价于"迁移到最新"的简化版，Demo 用足够）
        await db.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ 数据库已创建\n");

        // 2) 插设备 + 测点
        var device = new Device
        {
            Name = "1#温湿度传感器",
            Protocol = DeviceProtocol.Rtu,
            SlaveId = 1,
            PortName = "COM3",
            BaudRate = 9600,
            DataBits = 8,
            StopBits = 1,
            Parity = 0,
            Enabled = true
        };
        device.DataPoints.Add(new DataPoint
        {
            Name = "温度",
            Area = ModbusArea.InputRegister,
            Address = 0,
            DataType = DataValueType.UInt16,
            Scale = 0.1,
            Unit = "℃",
            DecimalPlaces = 1
        });
        device.DataPoints.Add(new DataPoint
        {
            Name = "湿度",
            Area = ModbusArea.InputRegister,
            Address = 1,
            DataType = DataValueType.UInt16,
            Scale = 0.1,
            Unit = "%RH",
            DecimalPlaces = 1
        });

        db.Devices.Add(device);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ 已插入设备 [{device.Name}]，测点 {device.DataPoints.Count} 个\n");

        // 3) 批量插采样
        var tempPoint = device.DataPoints.First(p => p.Name == "温度");
        var rnd = new Random(42);
        var samples = new List<Sample>();
        var now = DateTime.Now;

        for (int i = 0; i < 100; i++)
        {
            samples.Add(new Sample
            {
                DataPointId = tempPoint.Id,
                Timestamp = now.AddSeconds(i),
                Value = Math.Round(23.0 + rnd.NextDouble() * 4, 1),
                Quality = QualityFlag.Good
            });
        }

        db.Samples.AddRange(samples);
        await db.SaveChangesAsync();
        Console.WriteLine($"✅ 已批量插入 {samples.Count} 条采样\n");

        // 4) 查询历史
        var history = await db.Samples
            .Where(s => s.DataPointId == tempPoint.Id)
            .OrderBy(s => s.Timestamp)
            .ToListAsync();

        Console.WriteLine("=== 历史采样（前 5 条）===");
        foreach (var s in history.Take(5))
        {
            Console.WriteLine($"  {s.Timestamp:HH:mm:ss}  {s.Value:F1} ℃  ({s.Quality})");
        }
        Console.WriteLine($"  ... 共 {history.Count} 条\n");

        // 5) 聚合演示：算这批采样的 Avg/Min/Max
        var agg = history
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Avg = g.Average(x => x.Value),
                Min = g.Min(x => x.Value),
                Max = g.Max(x => x.Value),
                Count = g.Count()
            })
            .First();

        Console.WriteLine("=== 聚合结果 ===");
        Console.WriteLine($"  平均 {agg.Avg:F2} ℃，最低 {agg.Min:F1} ℃，最高 {agg.Max:F1} ℃，点数 {agg.Count}\n");

        Console.WriteLine("=== 所有映射的实体/表 ===");
        foreach (var et in db.Model.GetEntityTypes())
        {
            Console.WriteLine($"  {et.GetTableName()}  <- {et.ClrType.Name}");
        }

        Console.WriteLine("\n✅ 存储层链路验证通过\n");

        // 6) 报警引擎演示
        AlarmDemo.Run();

        // 7) 采集服务端到端演示（无硬件，用假设备客户端）
        await AcquisitionDemo.RunAsync(dbPath);
    }
}
