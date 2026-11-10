using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductMarkerRatioConfiguration : IEntityTypeConfiguration<ProductMarkerRatio>
{
    public void Configure(EntityTypeBuilder<ProductMarkerRatio> entity)
    {
        entity.ToTable("ProductMarkerRatio", tb =>
        {
            tb.HasComment("Tỉ lệ size của bìa: số sản phẩm mỗi size trên 1 lớp");
            tb.HasTrigger("trg_ProductMarkerRatio_Check");
        });

        entity.HasKey(e => new { e.ProductMarkerId, e.SizeCode });
        entity.Property(e => e.SizeCode).HasMaxLength(10).IsUnicode(false);

        entity.HasOne(d => d.ProductMarker).WithMany(p => p.ProductMarkerRatios)
            .HasForeignKey(d => d.ProductMarkerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PMR_Marker");
    }
}
