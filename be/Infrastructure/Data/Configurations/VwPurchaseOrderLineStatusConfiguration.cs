using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwPurchaseOrderLineStatusConfiguration : IEntityTypeConfiguration<VwPurchaseOrderLineStatus>
{
    public void Configure(EntityTypeBuilder<VwPurchaseOrderLineStatus> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_PurchaseOrderLineStatus");

        entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.NotReplenishedQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.OrderedQty).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.OutstandingQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.ReceivedQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.UnitPrice).HasColumnType("decimal(18, 2)");
    }
}
