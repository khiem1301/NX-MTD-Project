using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OutputRecordConfiguration : IEntityTypeConfiguration<OutputRecord>
{
    public void Configure(EntityTypeBuilder<OutputRecord> entity)
    {
        entity.ToTable("OutputRecord", tb =>
            {
                tb.HasComment("Sản lượng công đoạn");
                tb.HasTrigger("trg_OutputRecord_Check");
                tb.HasTrigger("trg_OutputRecord_PeriodLock");
            });

        entity.HasIndex(e => new { e.OrderLineId, e.OperationId }, "IX_OR_OrderLine");

        entity.HasIndex(e => new { e.WorkerId, e.WorkDate }, "IX_OR_Worker");

        entity.HasIndex(e => new { e.WorkDate, e.ShiftCode, e.SewingTeamId, e.OrderLineId, e.OperationId, e.WorkerId }, "UQ_OutputRecord").IsUnique();

        entity.Property(e => e.EditDeadline).HasPrecision(0);
        entity.Property(e => e.EnteredAt)
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
            .HasDefaultValue("SAVED");
        entity.Property(e => e.UpdatedAt).HasPrecision(0);

        entity.HasOne(d => d.Operation).WithMany(p => p.OutputRecords)
            .HasForeignKey(d => d.OperationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OR_Operation");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.OutputRecords)
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OR_OrderLine");

        entity.HasOne(d => d.PieceRate).WithMany(p => p.OutputRecords)
            .HasForeignKey(d => d.PieceRateId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OR_PieceRate");

        entity.HasOne(d => d.SewingTeam).WithMany(p => p.OutputRecords)
            .HasForeignKey(d => d.SewingTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OR_Team");

        entity.HasOne(d => d.Worker).WithMany(p => p.OutputRecords)
            .HasForeignKey(d => d.WorkerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OR_Worker");
    }
}
