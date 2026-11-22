using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceMonitor.Storage.Configurations;

/// <summary>
/// Sample 表配置（原始秒级采样）。
/// </summary>
/// <remarks>
/// 这是全库写入最频繁的表，索引必须为查询模式服务：
/// 历史查询永远是"按测点 + 按时间区间"，所以建 (DataPointId, Timestamp) 复合索引。
/// </remarks>
public class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.ToTable("T_Samples");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).HasConversion<double>();

        // 复合索引：历史查询主路径
        builder.HasIndex(x => new { x.DataPointId, x.Timestamp });
    }
}
