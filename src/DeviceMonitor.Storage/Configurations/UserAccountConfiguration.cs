using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceMonitor.Storage.Configurations;

/// <summary>
/// UserAccount 表配置。
/// </summary>
public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("T_Users");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserName).HasMaxLength(50).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Salt).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(100);

        builder.HasIndex(x => x.UserName).IsUnique();
    }
}
