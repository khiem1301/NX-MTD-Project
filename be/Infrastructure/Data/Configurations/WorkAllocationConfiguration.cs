using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class WorkAllocationConfiguration : IEntityTypeConfiguration<WorkAllocation>
{
    public void Configure(EntityTypeBuilder<WorkAllocation> entity)
    {
        entity.ToTable("WorkAllocation", tb =>
            {
                tb.HasComment("Giao / thu hồi việc với công nhân");
                tb.HasTrigger("trg_WorkAllocation_Check");
            });

        entity.HasIndex(e => new { e.SewingTeamId, e.OrderLineId, e.OperationId, e.Status }, "IX_WA_Line");

        entity.HasIndex(e => new { e.WorkerId, e.OrderLineId, e.OperationId, e.Status }, "IX_WA_Worker");

        entity.Property(e => e.ConfirmMode)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.ConfirmedAt).HasPrecision(0);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.DiscrepancyReason).HasMaxLength(500);
        entity.Property(e => e.MovementType)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.OnBehalfReason).HasMaxLength(500);
        entity.Property(e => e.RejectReason).HasMaxLength(500);
        entity.Property(e => e.RetrievalReason)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("PENDING");

        entity.HasOne(d => d.Operation).WithMany(p => p.WorkAllocations)
            .HasForeignKey(d => d.OperationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_WA_Operation");

        entity.HasOne(d => d.OrderLine).WithMany(p => p.WorkAllocations)
            .HasForeignKey(d => d.OrderLineId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_WA_OrderLine");

        entity.HasOne(d => d.SewingTeam).WithMany(p => p.WorkAllocations)
            .HasForeignKey(d => d.SewingTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_WA_Team");

        entity.HasOne(d => d.Worker).WithMany(p => p.WorkAllocations)
            .HasForeignKey(d => d.WorkerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_WA_Worker");
    }
}
