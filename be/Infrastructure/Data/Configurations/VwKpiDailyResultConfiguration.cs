using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwKpiDailyResultConfiguration : IEntityTypeConfiguration<VwKpiDailyResult>
{
    public void Configure(EntityTypeBuilder<VwKpiDailyResult> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_KpiDailyResult");

        entity.Property(e => e.DailyTarget).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.DefectRatePct).HasColumnType("decimal(5, 2)");
        entity.Property(e => e.PercentAchieved).HasColumnType("decimal(9, 2)");
    }
}
