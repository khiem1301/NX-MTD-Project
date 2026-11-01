using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> entity)
    {
        entity.HasKey(e => e.SettingKey);

        entity.ToTable("SystemSetting", tb => tb.HasComment("Tham số hệ thống"));

        entity.Property(e => e.SettingKey)
            .HasMaxLength(50)
            .IsUnicode(false);
        entity.Property(e => e.Description).HasMaxLength(500);
        entity.Property(e => e.SettingValue).HasMaxLength(200);
        entity.Property(e => e.UpdatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
    }
}
