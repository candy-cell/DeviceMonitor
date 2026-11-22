using DeviceMonitor.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeviceMonitor.Storage.Configurations;

/// <summary>
/// Device 表配置。
/// </summary>
public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("T_Devices");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PortName).HasMaxLength(20);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.Remark).HasMaxLength(500);

        // 从站地址做唯一约束？不行——RTU 总线上多台设备可能共享同一物理串口但地址唯一，
        // 但 TCP 不同 IP 可以有相同 SlaveId，所以不能全局唯一。这里只建索引加速查询。
        builder.HasIndex(x => x.Enabled);

        // 导航：一个设备多个测点；删除设备时级联删除测点
        builder.HasMany(x => x.DataPoints)
               .WithOne(x => x.Device)
               .HasForeignKey(x => x.DeviceId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
