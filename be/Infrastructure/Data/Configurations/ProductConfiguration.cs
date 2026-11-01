using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> entity)
    {
        entity.ToTable("Product", tb => tb.HasComment("Mã hàng"));

        entity.HasIndex(e => e.ProductCode, "UQ_Product_Code").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Description).HasMaxLength(1000);
        entity.Property(e => e.ProductCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.ProductName).HasMaxLength(200);
        entity.Property(e => e.SampleImageUrl).HasMaxLength(500);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
    }
}
