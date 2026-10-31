using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class MaterialIssueConfiguration : IEntityTypeConfiguration<MaterialIssue>
{
    public void Configure(EntityTypeBuilder<MaterialIssue> entity)
    {
        entity.ToTable("MaterialIssue", tb =>
            {
                tb.HasComment("Phiếu cấp kho: NVL cho tổ cắt/may hoặc BTP cho tổ may");
                tb.HasTrigger("trg_MaterialIssue_Check");
            });

        entity.HasIndex(e => new { e.ReceivingTeamId, e.Status }, "IX_MI_Team");

        entity.HasIndex(e => e.IssueCode, "UQ_MaterialIssue_Code").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.IssueCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.ReceivedAt).HasPrecision(0);
        entity.Property(e => e.RejectReason).HasMaxLength(500);
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("PENDING_RECEIPT");
        entity.Property(e => e.WarehouseType)
            .HasMaxLength(15)
            .IsUnicode(false)
            .HasDefaultValue("MATERIAL");

        entity.HasOne(d => d.IssuedByNavigation).WithMany(p => p.MaterialIssueIssuedByNavigations)
            .HasForeignKey(d => d.IssuedBy)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MI_IssuedBy");

        entity.HasOne(d => d.Order).WithMany(p => p.MaterialIssues)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MI_Order");

        entity.HasOne(d => d.ReceivedByNavigation).WithMany(p => p.MaterialIssueReceivedByNavigations)
            .HasForeignKey(d => d.ReceivedBy)
            .HasConstraintName("FK_MI_ReceivedBy");

        entity.HasOne(d => d.ReceivingTeam).WithMany(p => p.MaterialIssues)
            .HasForeignKey(d => d.ReceivingTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_MI_Team");

        entity.HasOne(d => d.Request).WithMany(p => p.MaterialIssues)
            .HasForeignKey(d => d.RequestId)
            .HasConstraintName("FK_MI_Request");

        entity.HasOne(d => d.WorkOrder).WithMany(p => p.MaterialIssues)
            .HasForeignKey(d => d.WorkOrderId)
            .HasConstraintName("FK_MI_WO");
    }
}
