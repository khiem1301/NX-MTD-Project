using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductMarkerConfiguration : IEntityTypeConfiguration<ProductMarker>
{
    public void Configure(EntityTypeBuilder<ProductMarker> entity)
    {
        // Có trigger trg_ProductMarker_Check: EF không dùng OUTPUT khi ghi.
        entity.ToTable("ProductMarker", tb =>
        {
            tb.HasComment("Bìa cắt của đơn: chiều dài, tỉ lệ size ở ProductMarkerRatio");
            tb.HasTrigger("trg_ProductMarker_Check");
        });

        entity.HasIndex(e => new { e.OrderId, e.ProductId, e.MarkerNo, e.MarkerCode }, "UQ_PM").IsUnique();
        entity.HasIndex(e => e.ProductId, "IX_PM_Product");

        entity.Property(e => e.MarkerCode).HasMaxLength(20).IsUnicode(false);
        entity.Property(e => e.LengthM).HasColumnType("decimal(7, 2)");
        entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false).HasDefaultValue("ACTIVE");

        entity.HasOne(d => d.Order).WithMany(p => p.ProductMarkers)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PM_Order");

        entity.HasOne(d => d.Product).WithMany(p => p.ProductMarkers)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PM_Product");
    }
}
