using DeviceMonitor.Core.Entities;

namespace DeviceMonitor.Core.Abstractions;

/// <summary>
/// 审计日志仓储接口。
/// </summary>
/// <remarks>
/// 每个方法自包含，内部创建独立 DbContext 并提交，天然线程安全。
/// </remarks>
public interface IAuditRepository
{
    /// <summary>写一条审计日志（立即保存）。</summary>
    Task AddAsync(AuditLog log);

    /// <summary>按时间倒序查最近 N 条。</summary>
    Task<List<AuditLog>> GetRecentAsync(int count = 100);
}
