using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductMarkerFabricConfiguration : IEntityTypeConfiguration<ProductMarkerFabric>
{
    public void Configure(EntityTypeBuilder<ProductMarkerFabric> entity)
    {
        entity.ToTable("ProductMarkerFabric", tb => tb.HasComment("Vải của bìa cắt theo màu áo"));

        entity.HasIndex(e => new { e.ProductId, e.MarkerNo, e.ProductColorId }, "UQ_PMF").IsUnique();
        entity.HasIndex(e => e.MaterialId, "IX_PMF_Material");

        entity.HasOne(d => d.ProductColor).WithMany(p => p.ProductMarkerFabrics)
            .HasPrincipalKey(p => new { p.ProductColorId, p.ProductId })
            .HasForeignKey(d => new { d.ProductColorId, d.ProductId })
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PMF_Color");

        entity.HasOne(d => d.Product).WithMany(p => p.ProductMarkerFabrics)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PMF_Product");

        entity.HasOne(d => d.Material).WithMany(p => p.ProductMarkerFabrics)
            .HasForeignKey(d => d.MaterialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PMF_Material");
    }
}
