namespace DeviceMonitor.Core.Entities;

/// <summary>
/// 聚合后的采样数据（分钟级 / 小时级）。
/// </summary>
/// <remarks>
/// 只存平均值是不够的——工程师需要看区间内的极值，所以同时保留 Min / Max / Count。
/// </remarks>
public class SampleAggregate
{
    /// <summary>测点 Id。</summary>
    public int DataPointId { get; set; }

    /// <summary>聚合桶的起始时刻（已对齐到分钟或小时）。</summary>
    public DateTime BucketTime { get; set; }

    /// <summary>桶内平均值。</summary>
    public double Avg { get; set; }

    /// <summary>桶内最小值。</summary>
    public double Min { get; set; }

    /// <summary>桶内最大值。</summary>
    public double Max { get; set; }

    /// <summary>桶内采样点数（用于判断该桶数据是否可信）。</summary>
    public int Count { get; set; }
}
