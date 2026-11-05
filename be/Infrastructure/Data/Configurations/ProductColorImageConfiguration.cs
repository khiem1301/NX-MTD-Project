using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductColorImageConfiguration : IEntityTypeConfiguration<ProductColorImage>
{
    public void Configure(EntityTypeBuilder<ProductColorImage> entity)
    {
        entity.ToTable("ProductColorImage", tb => tb.HasComment("Ảnh theo màu áo của mã hàng"));

        entity.HasIndex(e => new { e.ProductColorId, e.SortOrder }, "UQ_ProductColorImage_Sort").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.ImageKey).HasMaxLength(500);

        entity.HasOne(d => d.ProductColor).WithMany(p => p.ProductColorImages)
            .HasForeignKey(d => d.ProductColorId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_ProductColorImage_Color");
    }
}
