using DeviceMonitor.Core.Entities;

namespace DeviceMonitor.Core.Abstractions;

/// <summary>
/// 设备仓储接口（定义在领域层，实现在基础设施层）。
/// </summary>
/// <remarks>
/// 每个方法自包含：内部创建独立 DbContext、执行、提交，调用方无需关心连接管理。
/// 这样天然线程安全，避免跨线程共享同一个 DbContext。
/// </remarks>
public interface IDeviceRepository
{
    /// <summary>按 Id 查设备（含测点）。</summary>
    Task<Device?> GetByIdAsync(int id);

    /// <summary>查全部启用设备（含测点）。</summary>
    Task<List<Device>> GetEnabledAsync();

    /// <summary>查全部设备（含测点）。</summary>
    Task<List<Device>> GetAllAsync();

    /// <summary>新增设备（立即保存）。</summary>
    Task AddAsync(Device device);

    /// <summary>更新设备（立即保存）。</summary>
    Task UpdateAsync(Device device);

    /// <summary>删除设备（立即保存）。</summary>
    Task RemoveAsync(Device device);
}
