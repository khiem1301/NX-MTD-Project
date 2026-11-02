using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductMarkerConfiguration : IEntityTypeConfiguration<ProductMarker>
{
    public void Configure(EntityTypeBuilder<ProductMarker> entity)
    {
        entity.ToTable("ProductMarker", tb => tb.HasComment("Bìa cắt của mã hàng theo size"));

        entity.HasIndex(e => new { e.ProductId, e.MarkerNo, e.SizeCode }, "UQ_PM").IsUnique();

        entity.Property(e => e.LengthCm).HasColumnType("decimal(7, 2)");
        entity.Property(e => e.WidthCm).HasColumnType("decimal(7, 2)");
        entity.Property(e => e.SizeCode).HasMaxLength(10).IsUnicode(false);

        entity.HasOne(d => d.Product).WithMany(p => p.ProductMarkers)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PM_Product");
    }
}
