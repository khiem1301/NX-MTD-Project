using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwPurchaseOrderDeliveryConfiguration : IEntityTypeConfiguration<VwPurchaseOrderDelivery>
{
    public void Configure(EntityTypeBuilder<VwPurchaseOrderDelivery> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_PurchaseOrderDelivery");

        entity.Property(e => e.GoodsStatus)
            .HasMaxLength(25)
            .IsUnicode(false);
        entity.Property(e => e.PoCode)
            .HasMaxLength(30)
            .IsUnicode(false);
    }
}
