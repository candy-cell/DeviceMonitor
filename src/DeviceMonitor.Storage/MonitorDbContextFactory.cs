using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Storage;

/// <summary>
/// MonitorDbContext 工厂，实现 <see cref="IDbContextFactory{MonitorDbContext}"/>。
/// </summary>
/// <remarks>
/// 为什么用工厂而不是单例 DbContext？
/// EF Core 的 DbContext 不是线程安全的——采集线程、报警落库、UI 登录如果共用同一个实例，
/// 会抛 "A second operation was started on this context instance..."。
/// 工厂每次 CreateDbContext() 返回一个全新实例，用完即弃，天然线程安全。
/// 工厂本身是无状态的、线程安全的，可以注册为单例。
/// </remarks>
public sealed class MonitorDbContextFactory : IDbContextFactory<MonitorDbContext>
{
    private readonly string _connectionString;

    public MonitorDbContextFactory(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    /// <inheritdoc />
    public MonitorDbContext CreateDbContext()
    {
        var opts = new DbContextOptionsBuilder<MonitorDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        return new MonitorDbContext(opts);
    }

    /// <summary>静态便捷方法（控制台 Demo、命令行工具用）。</summary>
    public static MonitorDbContext Create(string dbPath)
        => new MonitorDbContextFactory(dbPath).CreateDbContext();
}
