using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> entity)
    {
        entity.ToTable("Material", tb => tb.HasComment("Nguyên vật liệu"));

        entity.HasIndex(e => e.MaterialName, "UQ_Material_Name").IsUnique();

        entity.Property(e => e.Color)
            .HasMaxLength(50)
            .HasDefaultValue("");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.MaterialName).HasMaxLength(200);
        entity.Property(e => e.MaterialType)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
        entity.Property(e => e.Unit).HasMaxLength(20);
        entity.Property(e => e.WidthM).HasColumnType("decimal(4, 2)");
    }
}
