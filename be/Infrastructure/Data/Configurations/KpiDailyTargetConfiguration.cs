using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class KpiDailyTargetConfiguration : IEntityTypeConfiguration<KpiDailyTarget>
{
    public void Configure(EntityTypeBuilder<KpiDailyTarget> entity)
    {
        entity.ToTable("KpiDailyTarget", tb => tb.HasComment("KPI tổ may theo từng ngày"));

        entity.HasIndex(e => new { e.KpiTargetId, e.WorkDate }, "UQ_KpiDaily").IsUnique();

        entity.HasOne(d => d.KpiTarget).WithMany(p => p.KpiDailyTargets)
            .HasForeignKey(d => d.KpiTargetId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_KpiDaily_Kpi");
    }
}
