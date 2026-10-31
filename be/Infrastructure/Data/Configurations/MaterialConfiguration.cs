using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> entity)
    {
        entity.ToTable("Material", tb => tb.HasComment("Nguyên vật liệu"));

        entity.HasIndex(e => e.MaterialCode, "UQ_Material_Code").IsUnique();

        entity.HasIndex(e => new { e.MaterialName, e.MaterialType, e.Color }, "UQ_Material_NameType").IsUnique();

        entity.Property(e => e.Category)
            .HasMaxLength(15)
            .IsUnicode(false);
        entity.Property(e => e.Color)
            .HasMaxLength(50)
            .HasDefaultValue("");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.LengthCm).HasColumnType("decimal(7, 2)");
        entity.Property(e => e.MaterialCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.MaterialName).HasMaxLength(200);
        entity.Property(e => e.MaterialType)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasComputedColumnSql("(CONVERT([varchar](10),case when [Category]='RIB' OR [Category]='INTERLINING' OR [Category]='LINING' OR [Category]='MAIN_FABRIC' then 'FABRIC' else 'TRIM' end))", true);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
        entity.Property(e => e.Unit).HasMaxLength(20);
        entity.Property(e => e.WidthCm).HasColumnType("decimal(7, 2)");
    }
}
