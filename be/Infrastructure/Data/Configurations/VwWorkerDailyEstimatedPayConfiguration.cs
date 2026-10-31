using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwWorkerDailyEstimatedPayConfiguration : IEntityTypeConfiguration<VwWorkerDailyEstimatedPay>
{
    public void Configure(EntityTypeBuilder<VwWorkerDailyEstimatedPay> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_WorkerDailyEstimatedPay");

        entity.Property(e => e.EmployeeCode)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.EstimatedPay).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.FullName).HasMaxLength(100);
        entity.Property(e => e.OutputAmount).HasColumnType("decimal(38, 2)");
        entity.Property(e => e.ScrapAmount).HasColumnType("decimal(38, 2)");
        entity.Property(e => e.SundayBonus).HasColumnType("decimal(18, 2)");
    }
}
