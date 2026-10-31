using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> entity)
    {
        entity.ToTable("PurchaseOrderLine", tb =>
            {
                tb.HasComment("Dòng đơn mua NVL");
                tb.HasTrigger("trg_PurchaseOrderLine_Check");
            });

        entity.HasIndex(e => new { e.PurchaseOrderId, e.OrderMaterialId }, "UQ_POL")
            .IsUnique()
            .HasFilter("([OrderMaterialId] IS NOT NULL)");

        entity.HasIndex(e => new { e.PurchaseOrderId, e.MaterialId }, "UQ_POL_Stock")
            .IsUnique()
            .HasFilter("([OrderMaterialId] IS NULL)");

        entity.Property(e => e.Amount)
            .HasComputedColumnSql("(CONVERT([decimal](18,2),[OrderedQty]*[UnitPrice]))", false)
            .HasColumnType("decimal(18, 2)");
        entity.Property(e => e.NotReplenishedQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.OrderedQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");

        entity.HasOne(d => d.Material).WithMany(p => p.PurchaseOrderLines)
            .HasForeignKey(d => d.MaterialId)
            .HasConstraintName("FK_POL_Material");

        entity.HasOne(d => d.OrderMaterial).WithMany(p => p.PurchaseOrderLines)
            .HasForeignKey(d => d.OrderMaterialId)
            .HasConstraintName("FK_POL_OrderMaterial");

        entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchaseOrderLines)
            .HasForeignKey(d => d.PurchaseOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_POL_PO");
    }
}
