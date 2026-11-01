using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OperationAssignmentConfiguration : IEntityTypeConfiguration<OperationAssignment>
{
    public void Configure(EntityTypeBuilder<OperationAssignment> entity)
    {
        entity.ToTable("OperationAssignment", tb =>
            {
                tb.HasComment("Phân công công đoạn");
                tb.HasTrigger("trg_OperationAssignment_Check");
            });

        entity.HasIndex(e => new { e.SewingTeamId, e.OrderId, e.OperationId, e.WorkerId }, "UX_OA_Current")
            .IsUnique()
            .HasFilter("([ValidTo] IS NULL)");

        entity.Property(e => e.AssignedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");

        entity.HasOne(d => d.Operation).WithMany(p => p.OperationAssignments)
            .HasForeignKey(d => d.OperationId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OA_Operation");

        entity.HasOne(d => d.Order).WithMany(p => p.OperationAssignments)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OA_Order");

        entity.HasOne(d => d.SewingTeam).WithMany(p => p.OperationAssignments)
            .HasForeignKey(d => d.SewingTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OA_Team");

        entity.HasOne(d => d.Worker).WithMany(p => p.OperationAssignments)
            .HasForeignKey(d => d.WorkerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OA_Worker");
    }
}
