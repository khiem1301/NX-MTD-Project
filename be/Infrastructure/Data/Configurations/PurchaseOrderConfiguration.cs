using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> entity)
    {
        entity.ToTable("PurchaseOrder", tb => tb.HasComment("Đơn mua NVL kèm thanh toán (OrderId NULL = đơn mua dùng chung)"));

        entity.HasIndex(e => e.PoCode, "UQ_PurchaseOrder_Code").IsUnique();

        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.GoodsStatus)
            .HasMaxLength(25)
            .IsUnicode(false)
            .HasDefaultValue("PENDING_FINALISATION");
        entity.Property(e => e.Note).HasMaxLength(1000);
        entity.Property(e => e.PaidAmount).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.PaymentMethod)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.PaymentNote).HasMaxLength(500);
        entity.Property(e => e.PaymentReturnReason).HasMaxLength(500);
        entity.Property(e => e.PaymentStatus)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("UNPAID");
        entity.Property(e => e.PoCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.RejectReason).HasMaxLength(500);
        entity.Property(e => e.RowVer)
            .IsRowVersion()
            .IsConcurrencyToken();

        entity.HasOne(d => d.Order).WithMany(p => p.PurchaseOrders)
            .HasForeignKey(d => d.OrderId)
            .HasConstraintName("FK_PO_Order");

        entity.HasOne(d => d.SourceRequest).WithMany(p => p.PurchaseOrders)
            .HasForeignKey(d => d.SourceRequestId)
            .HasConstraintName("FK_PO_Request");

        entity.HasOne(d => d.Supplier).WithMany(p => p.PurchaseOrders)
            .HasForeignKey(d => d.SupplierId)
            .HasConstraintName("FK_PO_Supplier");
    }
}
