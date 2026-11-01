using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class FgHandoverLineConfiguration : IEntityTypeConfiguration<FgHandoverLine>
{
    public void Configure(EntityTypeBuilder<FgHandoverLine> entity)
    {
        entity.ToTable("FgHandoverLine", tb =>
            {
                tb.HasComment("Dòng bàn giao thành phẩm");
                tb.HasTrigger("trg_FgHandoverLine_Check");
            });

        entity.HasIndex(e => new { e.FgHandoverId, e.OrderLineId }, "UQ_FGHL").IsUnique();

        entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);

        entity.HasOne(d => d.FgHandover).WithMany(p => p.FgHandoverLines)
            .HasForeignKey(d => d.FgHandoverId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_FGHL_Handover");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.FgHandoverLines)
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_FGHL_OrderLine");
    }
}
