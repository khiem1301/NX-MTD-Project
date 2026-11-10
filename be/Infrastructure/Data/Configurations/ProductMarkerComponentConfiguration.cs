using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class ProductMarkerComponentConfiguration : IEntityTypeConfiguration<ProductMarkerComponent>
{
    public void Configure(EntityTypeBuilder<ProductMarkerComponent> entity)
    {
        entity.ToTable("ProductMarkerComponent", tb => tb.HasComment("Chi tiết nằm trên nhóm bìa nào của mã hàng"));

        entity.HasKey(e => new { e.ProductId, e.MarkerNo, e.ProductComponentId });
        entity.HasIndex(e => e.ProductComponentId, "IX_PMC_Component");

        entity.HasOne(d => d.ProductComponent).WithMany(p => p.ProductMarkerComponents)
            .HasPrincipalKey(p => new { p.ProductComponentId, p.ProductId })
            .HasForeignKey(d => new { d.ProductComponentId, d.ProductId })
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PMC_Component");
    }
}
