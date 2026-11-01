using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwMaterialStockConfiguration : IEntityTypeConfiguration<VwMaterialStock>
{
    public void Configure(EntityTypeBuilder<VwMaterialStock> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_MaterialStock");

        entity.Property(e => e.AvailableQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.Category)
            .HasMaxLength(15)
            .IsUnicode(false);
        entity.Property(e => e.Color).HasMaxLength(50);
        entity.Property(e => e.MaterialCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.MaterialName).HasMaxLength(200);
        entity.Property(e => e.MaterialType)
            .HasMaxLength(10)
            .IsUnicode(false);
        entity.Property(e => e.OnHandQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.ReservedQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.Unit).HasMaxLength(20);
    }
}
