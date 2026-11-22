using DeviceMonitor.Core.Entities;

namespace DeviceMonitor.Core.Abstractions;

/// <summary>
/// 采样仓储接口。
/// </summary>
/// <remarks>
/// 每个方法自包含，内部创建独立 DbContext 并提交，天然线程安全。
/// </remarks>
public interface ISampleRepository
{
    /// <summary>批量写入采样（立即保存）。</summary>
    Task AddRangeAsync(IEnumerable<Sample> samples);

    /// <summary>按测点 + 时间区间查询历史采样。</summary>
    Task<List<Sample>> GetHistoryAsync(int dataPointId, DateTime from, DateTime to, int maxCount = 10000);

    /// <summary>删除指定时间之前的原始采样（配合降采样任务使用）。</summary>
    Task<int> DeleteBeforeAsync(DateTime cutoff);
}
