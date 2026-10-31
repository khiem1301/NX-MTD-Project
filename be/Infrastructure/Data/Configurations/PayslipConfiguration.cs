using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> entity)
    {
        entity.ToTable("Payslip", tb => tb.HasComment("Phiếu lương"));

        entity.HasIndex(e => new { e.PayrollId, e.EmployeeId }, "UQ_Payslip").IsUnique();

        entity.Property(e => e.Insurance).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.NetPay).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.TotalAdditions).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.TotalDeductions).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.WorkDays).HasColumnType("decimal(5, 2)");

        entity.HasOne(d => d.Employee).WithMany(p => p.Payslips)
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Payslip_Employee");

        entity.HasOne(d => d.Payroll).WithMany(p => p.Payslips)
            .HasForeignKey(d => d.PayrollId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Payslip_Payroll");
    }
}
