using DeviceMonitor.Alarms;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Demo;

/// <summary>
/// 报警引擎验证：演示阈值 → 死区 → 延时 → 状态机（触发/确认/恢复/峰值）全流程。
/// </summary>
internal static class AlarmDemo
{
    public static void Run()
    {
        Console.WriteLine("========== 报警引擎演示 ==========\n");

        var engine = new AlarmEngine();

        // 订阅事件，模拟"外部落库/刷界面"的接收方
        engine.Triggered += r =>
            Console.WriteLine($"  🔔 [触发] {r.Message}  (state={r.State}, level={r.Level})");
        engine.Acknowledged += r =>
            Console.WriteLine($"  ✅ [确认] 由 {r.AckUser} 在 {r.AckTime:HH:mm:ss} 确认  (state={r.State})");
        engine.Cleared += r =>
            Console.WriteLine($"  🔕 [恢复] 结束于 {r.EndTime:HH:mm:ss}，峰值 {r.PeakValue:F2}  (state={r.State})");

        // 规则：温度超上限 25℃，死区 1℃，延时 2 秒
        var rule = new AlarmRule
        {
            Id = 1,
            DataPointId = 7,
            Type = AlarmType.High,
            Threshold = 25.0,
            Deadband = 1.0,
            DelayMs = 2000,
            Level = AlarmLevel.Warning,
            Remark = "温度超上限"
        };

        var now = new DateTime(2026, 11, 21, 11, 0, 0);

        Console.WriteLine("规则：温度 > 25℃ 报警，死区 1℃，延时 2s\n");

        // ① 正常值，不报警
        Console.WriteLine("[1] 温度 24.0（正常）");
        engine.Evaluate(rule, 24.0, now);
        now = now.AddSeconds(1);

        // ② 超过阈值 25.8，开始计时（累计 0s，未满 2s → 不触发）
        Console.WriteLine("[2] 温度 25.8（超阈值，开始计时 0s）");
        engine.Evaluate(rule, 25.8, now);
        now = now.AddSeconds(1);

        // ③ 又过 1s，累计 1s，仍未满 2s → 不触发
        Console.WriteLine("[3] 温度 26.0（累计 1s，未满 2s）");
        engine.Evaluate(rule, 26.0, now);
        now = now.AddSeconds(1);

        // ④ 又过 1s，累计 2s → 触发报警（此时值是 27.5，作为触发值）
        Console.WriteLine("[4] 温度 27.5（累计 2s，触发）");
        engine.Evaluate(rule, 27.5, now);
        now = now.AddSeconds(1);

        // ⑤ 峰值追踪：升到 28.0，峰值更新
        Console.WriteLine("[5] 温度 28.0（峰值更新到 28.0）");
        engine.Evaluate(rule, 28.0, now);
        now = now.AddSeconds(1);

        // ⑥ 死区测试：降到 24.5，虽然低于阈值，但在死区内（25-1=24），不恢复
        Console.WriteLine("[6] 温度 24.5（死区内，不恢复）");
        engine.Evaluate(rule, 24.5, now);
        now = now.AddSeconds(1);

        // ⑦ 确认报警（还在 Active）
        Console.WriteLine("[7] 操作员确认报警");
        engine.Acknowledge(1, "张三", now);
        now = now.AddSeconds(1);

        // ⑧ 降到 23.0，低于死区下限 → 恢复
        Console.WriteLine("[8] 温度 23.0（低于死区下限，恢复）");
        engine.Evaluate(rule, 23.0, now);

        Console.WriteLine("\n========== 报警引擎演示结束 ==========\n");
    }
}
