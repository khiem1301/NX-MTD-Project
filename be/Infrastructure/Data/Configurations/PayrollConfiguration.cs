using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class PayrollConfiguration : IEntityTypeConfiguration<Payroll>
{
    public void Configure(EntityTypeBuilder<Payroll> entity)
    {
        entity.ToTable("Payroll", tb => tb.HasComment("Bảng lương tháng (import)"));

        entity.HasIndex(e => e.PeriodId, "UQ_Payroll_Period").IsUnique();

        entity.Property(e => e.ApprovedAt).HasPrecision(0);
        entity.Property(e => e.RejectReason).HasMaxLength(500);
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("DRAFT");
        entity.Property(e => e.SubmittedAt).HasPrecision(0);

        entity.HasOne(d => d.Period).WithOne(p => p.Payroll)
            .HasForeignKey<Payroll>(d => d.PeriodId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Payroll_Period");
    }
}
