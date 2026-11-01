using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> entity)
    {
        entity.ToTable("ProductImage", tb => tb.HasComment("Ảnh phụ của mã hàng (tối đa 5)"));

        entity.HasIndex(e => new { e.ProductId, e.SortOrder }, "UQ_ProductImage_Sort").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.ImageKey).HasMaxLength(500);

        entity.HasOne(d => d.Product).WithMany(p => p.ProductImages)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_ProductImage_Product");
    }
}
