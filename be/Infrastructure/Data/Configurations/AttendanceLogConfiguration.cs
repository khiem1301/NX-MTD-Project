using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class AttendanceLogConfiguration : IEntityTypeConfiguration<AttendanceLog>
{
    public void Configure(EntityTypeBuilder<AttendanceLog> entity)
    {
        entity.ToTable("AttendanceLog", tb =>
            {
                tb.HasComment("Lượt quẹt vân tay (3 trường lấy từ RJ)");
                tb.HasTrigger("trg_AttendanceLog_Guard");
            });

        entity.HasIndex(e => new { e.EmployeeId, e.WorkDate }, "IX_AL_Employee");

        entity.HasIndex(e => e.AttendanceCode, "IX_AL_Unmapped").HasFilter("([EmployeeId] IS NULL)");

        entity.HasIndex(e => new { e.AttendanceCode, e.PunchTime }, "UQ_AttendanceLog").IsUnique();

        entity.HasIndex(e => e.SourceRecordId, "UX_AL_SourceRecord")
            .IsUnique()
            .HasFilter("([SourceRecordId] IS NOT NULL)");

        entity.Property(e => e.PunchTime).HasPrecision(0);
        entity.Property(e => e.WorkDate).HasComputedColumnSql("(CONVERT([date],[PunchTime]))", true);

        entity.HasOne(d => d.Employee).WithMany(p => p.AttendanceLogs)
            .HasForeignKey(d => d.EmployeeId)
            .HasConstraintName("FK_AL_Employee");

        entity.HasOne(d => d.SyncSession).WithMany(p => p.AttendanceLogs)
            .HasForeignKey(d => d.SyncSessionId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_AL_Session");
    }
}
