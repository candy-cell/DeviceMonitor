using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceMonitor.Storage.Configurations;

/// <summary>
/// AlarmRecord 表配置。
/// </summary>
public class AlarmRecordConfiguration : IEntityTypeConfiguration<AlarmRecord>
{
    public void Configure(EntityTypeBuilder<AlarmRecord> builder)
    {
        builder.ToTable("T_AlarmRecords");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Message).HasMaxLength(500);
        builder.Property(x => x.AckUser).HasMaxLength(100);

        builder.Property(x => x.State).HasConversion<int>();
        builder.Property(x => x.Level).HasConversion<int>();

        // 报警查询主路径：按时间倒序
        builder.HasIndex(x => x.StartTime);
    }
}
