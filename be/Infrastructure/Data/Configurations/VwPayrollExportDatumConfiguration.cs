using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwPayrollExportDatumConfiguration : IEntityTypeConfiguration<VwPayrollExportDatum>
{
    public void Configure(EntityTypeBuilder<VwPayrollExportDatum> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_PayrollExportData");

        entity.Property(e => e.EmployeeCode)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.FullName).HasMaxLength(100);
        entity.Property(e => e.GradeAllowance).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.GradeCode)
            .HasMaxLength(5)
            .IsUnicode(false);
        entity.Property(e => e.OvertimeHours).HasColumnType("decimal(38, 2)");
        entity.Property(e => e.PayType)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.PeriodMonth)
            .HasMaxLength(7)
            .IsUnicode(false)
            .IsFixedLength();
        entity.Property(e => e.PieceRateAmount).HasColumnType("decimal(38, 2)");
        entity.Property(e => e.RoleName).HasMaxLength(100);
        entity.Property(e => e.ScrapDeduction).HasColumnType("decimal(38, 2)");
        entity.Property(e => e.SundayOvertimeHours).HasColumnType("decimal(38, 2)");
        entity.Property(e => e.SundayPieceRateBonus).HasColumnType("decimal(38, 2)");
        entity.Property(e => e.SundayWorkDays).HasColumnType("decimal(38, 2)");
        entity.Property(e => e.TeamName).HasMaxLength(100);
        entity.Property(e => e.WorkDays).HasColumnType("decimal(38, 2)");
    }
}
