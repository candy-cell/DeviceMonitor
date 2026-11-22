using DeviceMonitor.Core.Entities;

namespace DeviceMonitor.Core.Abstractions;

/// <summary>
/// 用户仓储接口。
/// </summary>
/// <remarks>
/// 每个方法自包含，内部创建独立 DbContext 并提交，天然线程安全。
/// </remarks>
public interface IUserRepository
{
    /// <summary>按登录名查用户。</summary>
    Task<UserAccount?> GetByUserNameAsync(string userName);

    /// <summary>查全部用户。</summary>
    Task<List<UserAccount>> GetAllAsync();

    /// <summary>新增用户（立即保存）。</summary>
    Task AddAsync(UserAccount user);

    /// <summary>更新用户（立即保存）。</summary>
    Task UpdateAsync(UserAccount user);
}
