using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class CutHandoverLineConfiguration : IEntityTypeConfiguration<CutHandoverLine>
{
    public void Configure(EntityTypeBuilder<CutHandoverLine> entity)
    {
        entity.ToTable("CutHandoverLine", tb =>
            {
                tb.HasComment("Dòng bàn giao BTP");
                tb.HasTrigger("trg_CutHandoverLine_Check");
            });

        entity.HasIndex(e => new { e.CutHandoverId, e.OrderLineId, e.ProductComponentMaterialId }, "UQ_CHL").IsUnique();

        entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);

        entity.HasOne(d => d.CutHandover).WithMany(p => p.CutHandoverLines)
            .HasForeignKey(d => d.CutHandoverId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CHL_Handover");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.CutHandoverLines)
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_CHL_OrderLine");

        entity.HasOne(d => d.ProductComponentMaterial).WithMany(p => p.CutHandoverLines)
            .HasForeignKey(d => d.ProductComponentMaterialId)
            .HasConstraintName("FK_CHL_PCM");
    }
}
