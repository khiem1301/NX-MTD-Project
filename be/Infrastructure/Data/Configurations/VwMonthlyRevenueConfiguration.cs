using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwMonthlyRevenueConfiguration : IEntityTypeConfiguration<VwMonthlyRevenue>
{
    public void Configure(EntityTypeBuilder<VwMonthlyRevenue> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_MonthlyRevenue");

        entity.Property(e => e.PeriodMonth).HasMaxLength(4000);
        entity.Property(e => e.Revenue).HasColumnType("decimal(38, 2)");
    }
}
