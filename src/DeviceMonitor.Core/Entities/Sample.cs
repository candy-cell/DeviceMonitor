using DeviceMonitor.Core.Enums;

namespace DeviceMonitor.Core.Entities;

/// <summary>
/// 一次采样记录（原始粒度，秒级）。
/// </summary>
/// <remarks>
/// 保留策略：原始数据只留最近若干天，超期由后台任务聚合成 <see cref="SampleAggregate"/> 后删除，
/// 避免秒级数据长期堆积把库撑爆。
/// </remarks>
public class Sample
{
    public long Id { get; set; }

    /// <summary>测点 Id。</summary>
    public int DataPointId { get; set; }

    /// <summary>采样时刻（本地时间）。</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>工程值（已按 Scale/Offset 换算）。</summary>
    public double Value { get; set; }

    /// <summary>数据质量。为 Bad 时 Value 不可信。</summary>
    public QualityFlag Quality { get; set; } = QualityFlag.Good;
}
