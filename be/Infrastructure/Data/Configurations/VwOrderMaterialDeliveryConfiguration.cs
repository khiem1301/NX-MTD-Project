using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwOrderMaterialDeliveryConfiguration : IEntityTypeConfiguration<VwOrderMaterialDelivery>
{
    public void Configure(EntityTypeBuilder<VwOrderMaterialDelivery> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_OrderMaterialDelivery");

        entity.Property(e => e.CustomerQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.ReceivedQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.RemainingQty).HasColumnType("decimal(38, 3)");
    }
}
