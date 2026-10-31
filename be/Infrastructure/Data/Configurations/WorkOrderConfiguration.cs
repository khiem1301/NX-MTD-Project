using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> entity)
    {
        entity.ToTable("WorkOrder", tb => tb.HasComment("Lệnh cắt"));

        entity.HasIndex(e => e.WorkOrderCode, "UQ_WorkOrder_Code").IsUnique();

        entity.Property(e => e.CompletedAt).HasPrecision(0);
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Note).HasMaxLength(1000);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("OPEN");
        entity.Property(e => e.WorkOrderCode)
            .HasMaxLength(30)
            .IsUnicode(false);

        entity.HasOne(d => d.CuttingTeam).WithMany(p => p.WorkOrders)
            .HasForeignKey(d => d.CuttingTeamId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_WO_Team");

        entity.HasOne(d => d.Order).WithMany(p => p.WorkOrders)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_WO_Order");

        entity.HasOne(d => d.Product).WithMany(p => p.WorkOrders)
            .HasForeignKey(d => d.ProductId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_WO_Product");

        entity.HasOne(d => d.SourceRequest).WithMany(p => p.WorkOrders)
            .HasForeignKey(d => d.SourceRequestId)
            .HasConstraintName("FK_WO_Request");
    }
}
