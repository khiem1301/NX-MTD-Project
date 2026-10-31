using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> entity)
    {
        entity.ToTable("Request", tb => tb.HasComment("Đơn từ (10 loại, gồm yêu cầu NVL / BTP)"));

        entity.HasIndex(e => new { e.CreatedBy, e.Status }, "IX_Request_CreatedBy");

        entity.HasIndex(e => new { e.RequestTypeCode, e.Status }, "IX_Request_Inbox");

        entity.HasIndex(e => e.RequestCode, "UQ_Request_Code").IsUnique();

        entity.HasIndex(e => e.DefectRecordId, "UX_Request_Defect")
            .IsUnique()
            .HasFilter("([DefectRecordId] IS NOT NULL)");

        entity.HasIndex(e => e.MaterialReceiptId, "UX_Request_Receipt")
            .IsUnique()
            .HasFilter("([MaterialReceiptId] IS NOT NULL AND [RequestTypeCode]='SHORT_DELIVERY')");

        entity.Property(e => e.CompletedAt).HasPrecision(0);
        entity.Property(e => e.Content).HasMaxLength(2000);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.DecidedAt).HasPrecision(0);
        entity.Property(e => e.DecisionNote).HasMaxLength(1000);
        entity.Property(e => e.RequestCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.RequestTypeCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.ResultNote).HasMaxLength(1000);
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();
        entity.Property(e => e.ShortageResolution)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.Title).HasMaxLength(200);
        entity.Property(e => e.UpdatedAt).HasPrecision(0);

        entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Requests)
            .HasForeignKey(d => d.CreatedBy)
            .HasConstraintName("FK_Request_CreatedBy");

        entity.HasOne(d => d.CreatedByTeam).WithMany(p => p.Requests)
            .HasForeignKey(d => d.CreatedByTeamId)
            .HasConstraintName("FK_Request_Team");

        entity.HasOne(d => d.DefectRecord).WithOne(p => p.Request)
            .HasForeignKey<Request>(d => d.DefectRecordId)
            .HasConstraintName("FK_Request_DefectRecord");

        entity.HasOne(d => d.KpiTarget).WithMany(p => p.Requests)
            .HasForeignKey(d => d.KpiTargetId)
            .HasConstraintName("FK_Request_Kpi");

        entity.HasOne(d => d.MaterialIssue).WithMany(p => p.Requests)
            .HasForeignKey(d => d.MaterialIssueId)
            .HasConstraintName("FK_Request_MaterialIssue");

        entity.HasOne(d => d.MaterialReceipt).WithOne(p => p.Request)
            .HasForeignKey<Request>(d => d.MaterialReceiptId)
            .HasConstraintName("FK_Request_MaterialReceipt");

        entity.HasOne(d => d.Order).WithMany(p => p.Requests)
            .HasForeignKey(d => d.OrderId)
            .HasConstraintName("FK_Request_Order");

        entity.HasOne(d => d.WorkOrder).WithMany(p => p.Requests)
            .HasForeignKey(d => d.WorkOrderId)
            .HasConstraintName("FK_Request_WorkOrder");
    }
}
