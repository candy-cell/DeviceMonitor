using DeviceMonitor.Core.Abstractions;
using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Storage;

/// <summary>
/// 用户仓储实现。每个方法独立 DbContext，线程安全。
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly IDbContextFactory<MonitorDbContext> _factory;

    public UserRepository(IDbContextFactory<MonitorDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<UserAccount?> GetByUserNameAsync(string userName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
    }

    public async Task<List<UserAccount>> GetAllAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Users.OrderBy(u => u.UserName).ToListAsync();
    }

    public async Task AddAsync(UserAccount user)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserAccount user)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Users.Update(user);
        await db.SaveChangesAsync();
    }
}
