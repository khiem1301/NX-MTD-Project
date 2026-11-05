using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductColorConfiguration : IEntityTypeConfiguration<ProductColor>
{
    public void Configure(EntityTypeBuilder<ProductColor> entity)
    {
        entity.ToTable("ProductColor", tb => tb.HasComment("Màu áo của mã hàng"));

        entity.HasIndex(e => new { e.ProductId, e.Color }, "UQ_PCol").IsUnique();

        entity.HasIndex(e => new { e.ProductColorId, e.ProductId }, "UQ_PCol_IdProduct").IsUnique();

        entity.Property(e => e.Color).HasMaxLength(50);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");

        entity.HasOne(d => d.Product).WithMany(p => p.ProductColors)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PCol_Product");
    }
}
