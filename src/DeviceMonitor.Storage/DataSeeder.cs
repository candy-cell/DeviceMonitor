using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;
using DeviceMonitor.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Storage;

/// <summary>
/// 种子数据初始化器：首次启动（数据库为空）时插入演示用的设备、测点和报警规则。
/// </summary>
/// <remarks>
/// 让 Shell 一跑起来就有东西可看，而不是对着空界面。
/// 只做"追加"不做"清空"，避免把用户真实数据冲掉。
///
/// 放在 Storage 层而非 Application 层，因为它依赖 EF Core 和 DbContext——
/// 而 Application 层刻意不引用 Storage（保持"应用层不依赖基础设施"的分层原则）。
/// </remarks>
public static class DataSeeder
{
    /// <summary>
    /// 若数据库没有任何设备，则插入一台模拟设备 + 两个测点 + 一条报警规则。
    /// 若数据库没有任何用户，则插入默认的 admin / operator 两个账号。
    /// </summary>
    /// <param name="db">数据库上下文。</param>
    /// <param name="deviceName">设备名（默认"模拟温湿度传感器"）。</param>
    public static async Task EnsureSeededAsync(MonitorDbContext db, string? deviceName = null)
    {
        await SeedUsersAsync(db);
        await SeedDeviceAsync(db, deviceName);
    }

    private static async Task SeedUsersAsync(MonitorDbContext db)
    {
        if (await db.Users.AnyAsync())
        {
            return;  // 已有用户，跳过
        }

        // 默认账号：admin / admin123（管理员），operator / operator123（操作员）
        // 密码只用 PBKDF2 哈希 + 盐存储，绝不存明文
        var (adminHash, adminSalt) = PasswordHasher.Hash("admin123");
        db.Users.Add(new UserAccount
        {
            UserName = "admin",
            PasswordHash = adminHash,
            Salt = adminSalt,
            IsAdmin = true,
            Enabled = true,
            DisplayName = "管理员"
        });

        var (opHash, opSalt) = PasswordHasher.Hash("operator123");
        db.Users.Add(new UserAccount
        {
            UserName = "operator",
            PasswordHash = opHash,
            Salt = opSalt,
            IsAdmin = false,
            Enabled = true,
            DisplayName = "操作员"
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDeviceAsync(MonitorDbContext db, string? deviceName)
    {
        if (await db.Devices.AnyAsync())
        {
            return;  // 已有数据，跳过
        }

        var device = new Device
        {
            Name = deviceName ?? "模拟温湿度传感器",
            Protocol = DeviceProtocol.Rtu,
            SlaveId = 1,
            PortName = "COM1",
            BaudRate = 9600,
            DataBits = 8,
            StopBits = 1,
            Parity = 0,
            PollIntervalMs = 1000,
            Enabled = true,
            Remark = "种子数据，模拟模式用"
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

        // 温度超上限报警规则（阈值 27℃，死区 0.5℃，延时 1.5s）
        var tempPoint = device.DataPoints.First(p => p.Name == "温度");
        db.AlarmRules.Add(new AlarmRule
        {
            DataPointId = tempPoint.Id,
            Type = AlarmType.High,
            Threshold = 27.0,
            Deadband = 0.5,
            DelayMs = 1500,
            Level = AlarmLevel.Warning,
            Enabled = true,
            Remark = "温度超上限"
        });

        await db.SaveChangesAsync();
    }
}
