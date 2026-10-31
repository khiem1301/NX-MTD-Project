using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwStockBalanceConfiguration : IEntityTypeConfiguration<VwStockBalance>
{
    public void Configure(EntityTypeBuilder<VwStockBalance> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_StockBalance");

        entity.Property(e => e.AvailableQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.OnHandQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.ReservedQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.WarehouseType)
            .HasMaxLength(15)
            .IsUnicode(false);
    }
}
