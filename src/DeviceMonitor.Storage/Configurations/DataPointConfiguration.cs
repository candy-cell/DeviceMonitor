using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceMonitor.Storage.Configurations;

/// <summary>
/// DataPoint 表配置。
/// </summary>
public class DataPointConfiguration : IEntityTypeConfiguration<DataPoint>
{
    public void Configure(EntityTypeBuilder<DataPoint> builder)
    {
        builder.ToTable("T_DataPoints");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(20);

        // 同一设备下的测点名不重复（便于按名字找测点）
        builder.HasIndex(x => new { x.DeviceId, x.Name }).IsUnique();

        // 枚举存 int（默认行为），但显式声明便于将来改字符串转换
        builder.Property(x => x.Area).HasConversion<int>();
        builder.Property(x => x.DataType).HasConversion<int>();

        // 常用查询：按设备 + 启用状态
        builder.HasIndex(x => new { x.DeviceId, x.Enabled });
    }
}
