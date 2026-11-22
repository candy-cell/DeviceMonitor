using DeviceMonitor.Core.Entities;

namespace DeviceMonitor.Core.Abstractions;

/// <summary>
/// 报警仓储接口。
/// </summary>
/// <remarks>
/// 每个方法自包含，内部创建独立 DbContext 并提交，天然线程安全。
/// </remarks>
public interface IAlarmRepository
{
    /// <summary>查全部启用的报警规则（含关联测点，用于换算与文案）。</summary>
    Task<List<AlarmRule>> GetEnabledRulesAsync();

    /// <summary>查指定测点的全部启用规则。</summary>
    Task<List<AlarmRule>> GetRulesByDataPointAsync(int dataPointId);

    /// <summary>新增一条报警记录（立即保存）。</summary>
    Task AddRecordAsync(AlarmRecord record);

    /// <summary>更新一条报警记录（立即保存）。</summary>
    Task UpdateRecordAsync(AlarmRecord record);

    /// <summary>按 Id 查报警记录。</summary>
    Task<AlarmRecord?> GetRecordAsync(long id);

    /// <summary>查当前活跃（未恢复）的报警。</summary>
    Task<List<AlarmRecord>> GetActiveRecordsAsync();
}
