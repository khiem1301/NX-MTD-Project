using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class PurchasePaymentConfiguration : IEntityTypeConfiguration<PurchasePayment>
{
    public void Configure(EntityTypeBuilder<PurchasePayment> entity)
    {
        entity.ToTable("PurchasePayment", tb =>
        {
            tb.HasComment("Từng lần thanh toán đơn mua: không xóa, sửa trong 24 giờ kèm lý do");
            tb.HasCheckConstraint("CK_PP_Amount", "Amount > 0");
        });

        entity.HasIndex(e => e.PurchaseOrderId, "IX_PP_PurchaseOrder");

        entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.CreatedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.UpdatedAt).HasPrecision(0);
        entity.Property(e => e.EditReason).HasMaxLength(TextLimits.Reason);
        entity.Property(e => e.PaymentMethod)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.PaymentNote).HasMaxLength(500);

        entity.HasOne(d => d.PurchaseOrder).WithMany(p => p.PurchasePayments)
            .HasForeignKey(d => d.PurchaseOrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_PP_PurchaseOrder");
    }
}
