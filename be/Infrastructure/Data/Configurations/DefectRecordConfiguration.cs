using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class DefectRecordConfiguration : IEntityTypeConfiguration<DefectRecord>
{
    public void Configure(EntityTypeBuilder<DefectRecord> entity)
    {
        entity.ToTable("DefectRecord", tb =>
            {
                tb.HasComment("Hàng lỗi Rework/Scrap và người gây lỗi");
                tb.HasTrigger("trg_DefectRecord_Check");
                tb.HasTrigger("trg_DefectRecord_PeriodLock");
            });

        entity.HasIndex(e => new { e.OrderLineId, e.DefectType }, "IX_DR_OrderLine");

        entity.HasIndex(e => new { e.ResponsibleWorkerId, e.WorkDate }, "IX_DR_Worker");

        entity.HasIndex(e => e.DefectCode, "UQ_DefectRecord_Code").IsUnique();

        entity.Property(e => e.DefectCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.DefectReasonCode)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.DefectType)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.EditDeadline).HasPrecision(0);
        entity.Property(e => e.ObjectedAt).HasPrecision(0);
        entity.Property(e => e.ReasonNote).HasMaxLength(500);
        entity.Property(e => e.RecordedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();
        entity.Property(e => e.ShiftCode)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("RECORDED");
        entity.Property(e => e.UpdatedAt).HasPrecision(0);
        entity.Property(e => e.WorkerNotifiedAt).HasPrecision(0);
        entity.Property(e => e.WorkerObjection).HasMaxLength(500);

        entity.HasOne(d => d.Operation).WithMany(p => p.DefectRecords)
            .HasForeignKey(d => d.OperationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_DR_Operation");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.DefectRecords)
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_DR_OrderLine");

        entity.HasOne(d => d.PieceRate).WithMany(p => p.DefectRecords)
            .HasForeignKey(d => d.PieceRateId)
            .HasConstraintName("FK_DR_PieceRate");

        entity.HasOne(d => d.RecordedByNavigation).WithMany(p => p.DefectRecordRecordedByNavigations)
            .HasForeignKey(d => d.RecordedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_DR_RecordedBy");

        entity.HasOne(d => d.ResponsibleWorker).WithMany(p => p.DefectRecordResponsibleWorkers)
            .HasForeignKey(d => d.ResponsibleWorkerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_DR_Worker");

        entity.HasOne(d => d.SewingTeam).WithMany(p => p.DefectRecords)
            .HasForeignKey(d => d.SewingTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_DR_Team");
    }
}
