using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class WorkOrderLineConfiguration : IEntityTypeConfiguration<WorkOrderLine>
{
    public void Configure(EntityTypeBuilder<WorkOrderLine> entity)
    {
        entity.ToTable("WorkOrderLine", tb =>
            {
                tb.HasComment("Dòng lệnh cắt");
                tb.HasTrigger("trg_WorkOrderLine_Check");
            });

        entity.HasIndex(e => new { e.WorkOrderId, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_WOL").IsUnique();

        entity.HasOne(d => d.OrderLine).WithMany(p => p.WorkOrderLines)
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_WOL_OrderLine");

        entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.WorkOrderLines)
            .HasForeignKey(d => d.ProductComponentMaterialId)
            .HasConstraintName("FK_WOL_PCM");

        entity.HasOne(d => d.WorkOrder).WithMany(p => p.WorkOrderLines)
            .HasForeignKey(d => d.WorkOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_WOL_WO");
    }
}
