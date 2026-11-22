using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using DeviceMonitor.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Storage;

/// <summary>
/// 报警仓储实现。每个方法独立 DbContext，线程安全。
/// </summary>
public class AlarmRepository : IAlarmRepository
{
    private readonly IDbContextFactory<MonitorDbContext> _factory;

    public AlarmRepository(IDbContextFactory<MonitorDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<AlarmRule>> GetEnabledRulesAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AlarmRules.Include(r => r.DataPoint).Where(r => r.Enabled).ToListAsync();
    }

    public async Task<List<AlarmRule>> GetRulesByDataPointAsync(int dataPointId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AlarmRules.Include(r => r.DataPoint)
             .Where(r => r.DataPointId == dataPointId && r.Enabled)
             .ToListAsync();
    }

    public async Task AddRecordAsync(AlarmRecord record)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.AlarmRecords.Add(record);
        await db.SaveChangesAsync();
    }

    public async Task UpdateRecordAsync(AlarmRecord record)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.AlarmRecords.Update(record);
        await db.SaveChangesAsync();
    }

    public async Task<AlarmRecord?> GetRecordAsync(long id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AlarmRecords.FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<AlarmRecord>> GetActiveRecordsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.AlarmRecords
             .Where(r => r.State != AlarmState.Cleared)
             .OrderByDescending(r => r.StartTime)
             .ToListAsync();
    }
}
