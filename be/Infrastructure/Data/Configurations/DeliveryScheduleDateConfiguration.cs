using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class DeliveryScheduleDateConfiguration : IEntityTypeConfiguration<DeliveryScheduleDate>
{
    public void Configure(EntityTypeBuilder<DeliveryScheduleDate> entity)
    {
        entity.ToTable("DeliveryScheduleDate", tb =>
        {
            tb.HasComment("Ngày dự kiến giao từng lần của đơn mua / đơn hàng khách giao / đơn giao bù (đúng một chủ lịch)");
            tb.HasCheckConstraint("CK_DSD_Owner",
                "(CASE WHEN PurchaseOrderId IS NULL THEN 0 ELSE 1 END + CASE WHEN OrderId IS NULL THEN 0 ELSE 1 END + CASE WHEN RequestId IS NULL THEN 0 ELSE 1 END) = 1");
            tb.HasCheckConstraint("CK_DSD_Seq", "SeqNo >= 1");
        });

        entity.HasIndex(e => new { e.PurchaseOrderId, e.SeqNo }, "UQ_DSD_PurchaseOrder").IsUnique().HasFilter("[PurchaseOrderId] IS NOT NULL");
        entity.HasIndex(e => new { e.OrderId, e.SeqNo }, "UQ_DSD_Order").IsUnique().HasFilter("[OrderId] IS NOT NULL");
        entity.HasIndex(e => new { e.RequestId, e.SeqNo }, "UQ_DSD_Request").IsUnique().HasFilter("[RequestId] IS NOT NULL");

        entity.HasOne(d => d.PurchaseOrder).WithMany()
            .HasForeignKey(d => d.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_DSD_PurchaseOrder");
        entity.HasOne(d => d.Order).WithMany()
            .HasForeignKey(d => d.OrderId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_DSD_Order");
        entity.HasOne(d => d.Request).WithMany()
            .HasForeignKey(d => d.RequestId).OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_DSD_Request");
    }
}
