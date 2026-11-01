using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialReceiptLineConfiguration : IEntityTypeConfiguration<MaterialReceiptLine>
{
    public void Configure(EntityTypeBuilder<MaterialReceiptLine> entity)
    {
        entity.ToTable("MaterialReceiptLine", tb =>
            {
                tb.HasComment("Dòng phiếu nhập NVL");
                tb.HasTrigger("trg_MaterialReceiptLine_Check");
            });

        entity.HasIndex(e => new { e.MaterialReceiptId, e.OrderMaterialId }, "UQ_MRL")
            .IsUnique()
            .HasFilter("([OrderMaterialId] IS NOT NULL)");

        entity.HasIndex(e => new { e.MaterialReceiptId, e.MaterialId }, "UQ_MRL_Stock")
            .IsUnique()
            .HasFilter("([OrderMaterialId] IS NULL)");

        entity.Property(e => e.AcceptedQty).HasColumnType("decimal(18, 3)");

        entity.HasOne(d => d.Material).WithMany(p => p.MaterialReceiptLines)
            .HasForeignKey(d => d.MaterialId)
            .HasConstraintName("FK_MRL_Material");

        entity.HasOne(d => d.MaterialReceipt).WithMany(p => p.MaterialReceiptLines)
            .HasForeignKey(d => d.MaterialReceiptId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MRL_Receipt");

        entity.HasOne(d => d.OrderMaterial).WithMany(p => p.MaterialReceiptLines)
            .HasForeignKey(d => d.OrderMaterialId)
            .HasConstraintName("FK_MRL_OrderMaterial");

        entity.HasOne(d => d.OriginalReceiptLine).WithMany(p => p.InverseOriginalReceiptLine)
            .HasForeignKey(d => d.OriginalReceiptLineId)
            .HasConstraintName("FK_MRL_OrigLine");

        entity.HasOne(d => d.PurchaseOrderLine).WithMany(p => p.MaterialReceiptLines)
            .HasForeignKey(d => d.PurchaseOrderLineId)
            .HasConstraintName("FK_MRL_POL");
    }
}
