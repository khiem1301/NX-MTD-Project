using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OrderPaymentConfiguration : IEntityTypeConfiguration<OrderPayment>
{
    public void Configure(EntityTypeBuilder<OrderPayment> entity)
    {
        entity.ToTable("OrderPayment", tb =>
        {
            tb.HasComment("Từng lần thanh toán của đơn hàng khách: số dương = khách trả, số âm = mình trả (đền hợp đồng); không xóa, sửa trong 24 giờ kèm lý do");
            tb.HasCheckConstraint("CK_OP_Amount", "Amount <> 0");
        });

        entity.HasIndex(e => e.OrderId, "IX_OP_Order");

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

        entity.HasOne(d => d.Order).WithMany(p => p.OrderPayments)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OP_Order");
    }
}
