using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceMonitor.Storage.Configurations;

/// <summary>
/// AuditLog 表配置。
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("T_AuditLogs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserName).HasMaxLength(100);
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Target).HasMaxLength(500);
        builder.Property(x => x.Result).HasMaxLength(500);

        builder.HasIndex(x => x.Timestamp);
    }
}
