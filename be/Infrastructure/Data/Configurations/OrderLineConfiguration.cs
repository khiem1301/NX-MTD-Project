using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> entity)
    {
        entity.ToTable("OrderLine", tb => tb.HasComment("Dòng đơn mã hàng–màu–size"));

        entity.HasIndex(e => e.ProductId, "IX_OrderLine_Product");

        entity.HasIndex(e => new { e.OrderId, e.ProductVariantId }, "UQ_OrderLine").IsUnique();

        entity.Property(e => e.CmtUnitPrice).HasColumnType("decimal(18, 2)");

        entity.HasOne(d => d.Order).WithMany(p => p.OrderLines)
            .HasForeignKey(d => d.OrderId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OrderLine_Order");

        entity.HasOne(d => d.ProductVariant).WithMany(p => p.OrderLines)
            .HasPrincipalKey(p => new { p.ProductVariantId, p.ProductId })
            .HasForeignKey(d => new { d.ProductVariantId, d.ProductId })
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_OrderLine_Variant");
    }
}
