using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Storage;

/// <summary>
/// 审计日志仓储实现。每个方法独立 DbContext，线程安全。
/// </summary>
public class AuditRepository : IAuditRepository
{
    private readonly IDbContextFactory<MonitorDbContext> _factory;

    public AuditRepository(IDbContextFactory<MonitorDbContext> factory)
    {
        _factory = factory;
    }

    public async Task AddAsync(AuditLog log)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetRecentAsync(int count = 100)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AuditLogs.OrderByDescending(l => l.Timestamp).Take(count).ToListAsync();
    }
}
