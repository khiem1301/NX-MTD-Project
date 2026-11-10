using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductComponentMaterialConfiguration : IEntityTypeConfiguration<ProductComponentMaterial>
{
    public void Configure(EntityTypeBuilder<ProductComponentMaterial> entity)
    {
        entity.ToTable("ProductComponentMaterial", tb => tb.HasComment("Định mức NVL theo màu áo × thành phần: dùng NVL nào (đã gồm màu NVL)"));

        entity.HasIndex(e => e.MaterialId, "IX_PCM_Material");

        entity.HasIndex(e => new { e.ProductColorId, e.ProductComponentId, e.MaterialId }, "UQ_PCM").IsUnique();

        entity.Property(e => e.SpecNote).HasMaxLength(200);

        entity.HasOne(d => d.Material).WithMany(p => p.ProductComponentMaterials)
            .HasForeignKey(d => d.MaterialId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PCM_Material");

        entity.HasOne(d => d.ProductComponent).WithMany(p => p.ProductComponentMaterials)
            .HasPrincipalKey(p => new { p.ProductComponentId, p.ProductId })
            .HasForeignKey(d => new { d.ProductComponentId, d.ProductId })
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PCM_Component");

        entity.HasOne(d => d.ProductColor).WithMany(p => p.ProductComponentMaterials)
            .HasPrincipalKey(p => new { p.ProductColorId, p.ProductId })
            .HasForeignKey(d => new { d.ProductColorId, d.ProductId })
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PCM_Color");
    }
}
