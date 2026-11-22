using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceMonitor.Storage.Configurations;

/// <summary>
/// SampleAggregate 表配置（分钟/小时级聚合）。
/// </summary>
public class SampleAggregateConfiguration : IEntityTypeConfiguration<SampleAggregate>
{
    public void Configure(EntityTypeBuilder<SampleAggregate> builder)
    {
        builder.ToTable("T_SampleAggregates");

        // 无自增主键，用 (DataPointId, BucketTime) 作为复合主键
        builder.HasKey(x => new { x.DataPointId, x.BucketTime });
    }
}
