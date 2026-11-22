using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Storage;

/// <summary>
/// 设备仓储实现（基础设施层）。
/// </summary>
/// <remarks>
/// 依赖 <see cref="IDbContextFactory{MonitorDbContext}"/> 而非 DbContext 本身——
/// 每个方法创建独立 DbContext、用完即弃，避免跨线程共享同一个实例。
/// </remarks>
public class DeviceRepository : IDeviceRepository
{
    private readonly IDbContextFactory<MonitorDbContext> _factory;

    public DeviceRepository(IDbContextFactory<MonitorDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<Device?> GetByIdAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Devices.Include(d => d.DataPoints).FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<List<Device>> GetEnabledAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Devices.Include(d => d.DataPoints).Where(d => d.Enabled).ToListAsync();
    }

    public async Task<List<Device>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Devices.Include(d => d.DataPoints).ToListAsync();
    }

    public async Task AddAsync(Device device)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Devices.Add(device);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Device device)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Devices.Update(device);
        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(Device device)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Devices.Remove(device);
        await db.SaveChangesAsync();
    }
}
