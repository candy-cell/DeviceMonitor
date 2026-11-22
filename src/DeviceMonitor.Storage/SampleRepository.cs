using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Storage;

/// <summary>
/// 采样仓储实现。每个方法独立 DbContext，线程安全。
/// </summary>
public class SampleRepository : ISampleRepository
{
    private readonly IDbContextFactory<MonitorDbContext> _factory;

    public SampleRepository(IDbContextFactory<MonitorDbContext> factory)
    {
        _factory = factory;
    }

    public async Task AddRangeAsync(IEnumerable<Sample> samples)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Samples.AddRange(samples);
        await db.SaveChangesAsync();
    }

    public async Task<List<Sample>> GetHistoryAsync(
        int dataPointId, DateTime from, DateTime to, int maxCount = 10000)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Samples
            .Where(s => s.DataPointId == dataPointId && s.Timestamp >= from && s.Timestamp <= to)
            .OrderBy(s => s.Timestamp)
            .Take(maxCount)
            .ToListAsync();
    }

    public async Task<int> DeleteBeforeAsync(DateTime cutoff)
    {
        await using var db = await _factory.CreateDbContextAsync();

        // SQLite 不擅长超大范围 DELETE，这里分页删，避免一次性锁太久
        int total = 0;
        while (true)
        {
            var batch = await db.Samples
                .Where(s => s.Timestamp < cutoff)
                .OrderBy(s => s.Timestamp)
                .Take(1000)
                .ToListAsync();

            if (batch.Count == 0) break;

            db.Samples.RemoveRange(batch);
            total += await db.SaveChangesAsync();
        }
        return total;
    }
}
