using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class KpiTargetLineConfiguration : IEntityTypeConfiguration<KpiTargetLine>
{
    public void Configure(EntityTypeBuilder<KpiTargetLine> entity)
    {
        entity.ToTable("KpiTargetLine", tb => tb.HasComment("KPI tổ may theo dòng đơn (màu × size)"));

        entity.HasIndex(e => new { e.KpiTargetId, e.OrderLineId }, "UQ_KpiTargetLine").IsUnique();
        entity.HasIndex(e => e.OrderLineId, "IX_KpiLine_OrderLine");

        entity.HasOne(d => d.KpiTarget).WithMany(p => p.KpiTargetLines)
            .HasForeignKey(d => d.KpiTargetId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_KpiLine_Kpi");

        entity.HasOne(d => d.OrderLine).WithMany()
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_KpiLine_OrderLine");
    }
}
