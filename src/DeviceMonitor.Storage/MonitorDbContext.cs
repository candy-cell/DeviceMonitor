using DeviceMonitor.Core.Entities;
using DeviceMonitor.Storage.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Storage;

/// <summary>
/// 上位机数据库上下文（SQLite）。
/// </summary>
/// <remarks>
/// 约定：
/// - 配置类用 <see cref="IEntityTypeConfiguration{T}"/> 独立到 Configurations 目录，不在 OnModelCreating 里堆 SQL；
/// - 表名用 T_ 前缀，避免与 SQLite 内部表（sqlite_sequence 等）混淆。
/// </remarks>
public class MonitorDbContext : DbContext
{
    public MonitorDbContext(DbContextOptions<MonitorDbContext> options) : base(options)
    {
    }

    // ---- 业务表 ----
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DataPoint> DataPoints => Set<DataPoint>();
    public DbSet<Sample> Samples => Set<Sample>();
    public DbSet<SampleAggregate> SampleAggregates => Set<SampleAggregate>();
    public DbSet<AlarmRule> AlarmRules => Set<AlarmRule>();
    public DbSet<AlarmRecord> AlarmRecords => Set<AlarmRecord>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MonitorDbContext).Assembly);
    }
}
