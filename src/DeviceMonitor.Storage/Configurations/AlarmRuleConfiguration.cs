using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceMonitor.Storage.Configurations;

/// <summary>
/// AlarmRule 表配置。
/// </summary>
public class AlarmRuleConfiguration : IEntityTypeConfiguration<AlarmRule>
{
    public void Configure(EntityTypeBuilder<AlarmRule> builder)
    {
        builder.ToTable("T_AlarmRules");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Remark).HasMaxLength(500);

        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.Level).HasConversion<int>();

        builder.HasIndex(x => x.DataPointId);
    }
}
