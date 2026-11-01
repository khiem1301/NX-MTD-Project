using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> entity)
    {
        entity.ToTable("ProductVariant", tb => tb.HasComment("Biến thể mã hàng: màu × size"));

        entity.HasIndex(e => new { e.ProductId, e.Color, e.SizeCode }, "UQ_PV").IsUnique();

        entity.HasIndex(e => new { e.ProductVariantId, e.ProductId }, "UQ_PV_IdProduct").IsUnique();

        entity.Property(e => e.Color).HasMaxLength(50);
        entity.Property(e => e.LengthCm).HasColumnType("decimal(7, 2)");
        entity.Property(e => e.SizeCode)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
        entity.Property(e => e.WidthCm).HasColumnType("decimal(7, 2)");

        entity.HasOne(d => d.Product).WithMany(p => p.ProductVariants)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PV_Product");
    }
}
