using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class AttendanceAdjustmentConfiguration : IEntityTypeConfiguration<AttendanceAdjustment>
{
    public void Configure(EntityTypeBuilder<AttendanceAdjustment> entity)
    {
        entity.ToTable("AttendanceAdjustment", tb =>
            {
                tb.HasComment("Chấm hộ của Kế toán");
                tb.HasTrigger("trg_AttendanceAdjustment_PeriodLock");
            });

        entity.HasIndex(e => new { e.EmployeeId, e.WorkDate }, "UQ_AA").IsUnique();

        entity.Property(e => e.AdjustmentType)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.CheckIn).HasPrecision(0);
        entity.Property(e => e.CheckOut).HasPrecision(0);
        entity.Property(e => e.Reason).HasMaxLength(500);
        entity.Property(e => e.RecordedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");

        entity.HasOne(d => d.Employee).WithMany(p => p.AttendanceAdjustments)
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_AA_Employee");

        entity.HasOne(d => d.Request).WithMany(p => p.AttendanceAdjustments)
            .HasForeignKey(d => d.RequestId)
            .HasConstraintName("FK_AA_Request");
    }
}
