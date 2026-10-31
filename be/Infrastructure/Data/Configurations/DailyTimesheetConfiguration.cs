using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class DailyTimesheetConfiguration : IEntityTypeConfiguration<DailyTimesheet>
{
    public void Configure(EntityTypeBuilder<DailyTimesheet> entity)
    {
        entity.ToTable("DailyTimesheet", tb => tb.HasComment("Bảng công ngày"));

        entity.HasIndex(e => e.WorkDate, "IX_DT_WorkDate");

        entity.HasIndex(e => new { e.EmployeeId, e.WorkDate }, "UQ_DailyTimesheet").IsUnique();

        entity.Property(e => e.CheckIn).HasPrecision(0);
        entity.Property(e => e.CheckOut).HasPrecision(0);
        entity.Property(e => e.IsSunday).HasComputedColumnSql("(CONVERT([bit],case when datediff(day,(0),[WorkDate])%(7)=(6) then (1) else (0) end))", true);
        entity.Property(e => e.Note).HasMaxLength(300);
        entity.Property(e => e.OvertimeHours).HasColumnType("decimal(5, 2)");
        entity.Property(e => e.Source)
            .HasMaxLength(15)
            .IsUnicode(false)
            .HasComputedColumnSql("(CONVERT([varchar](15),case when [AttendanceAdjustmentId] IS NULL then 'FINGERPRINT' else 'ADJUSTMENT' end))", true);
        entity.Property(e => e.UpdatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.WorkdayValue).HasColumnType("decimal(4, 2)");
        entity.Property(e => e.WorkedHours).HasColumnType("decimal(5, 2)");

        entity.HasOne(d => d.AttendanceAdjustment).WithMany(p => p.DailyTimesheets)
            .HasForeignKey(d => d.AttendanceAdjustmentId)
            .HasConstraintName("FK_DT_Adjustment");

        entity.HasOne(d => d.Employee).WithMany(p => p.DailyTimesheets)
            .HasForeignKey(d => d.EmployeeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_DT_Employee");
    }
}
