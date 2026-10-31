using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class QcBatchConfiguration : IEntityTypeConfiguration<QcBatch>
{
    public void Configure(EntityTypeBuilder<QcBatch> entity)
    {
        entity.ToTable("QcBatch", tb =>
            {
                tb.HasComment("Đợt kiểm hoàn thiện");
                tb.HasTrigger("trg_QcBatch_PeriodLock");
            });

        entity.HasIndex(e => e.OrderLineId, "IX_QC_OrderLine");

        entity.HasIndex(e => new { e.WorkDate, e.ShiftCode, e.BatchTime, e.SewingTeamId, e.OrderLineId }, "UQ_QcBatch").IsUnique();

        entity.Property(e => e.BatchTime).HasPrecision(0);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.EditDeadline).HasPrecision(0);
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();
        entity.Property(e => e.ShiftCode)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.UpdatedAt).HasPrecision(0);

        entity.HasOne(d => d.Inspector).WithMany(p => p.QcBatches)
            .HasForeignKey(d => d.InspectorId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_QC_Inspector");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.QcBatches)
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_QC_OrderLine");

        entity.HasOne(d => d.SewingTeam).WithMany(p => p.QcBatches)
            .HasForeignKey(d => d.SewingTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_QC_Team");
    }
}
