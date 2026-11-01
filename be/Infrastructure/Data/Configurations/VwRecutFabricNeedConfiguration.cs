using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwRecutFabricNeedConfiguration : IEntityTypeConfiguration<VwRecutFabricNeed>
{
    public void Configure(EntityTypeBuilder<VwRecutFabricNeed> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_RecutFabricNeed");

        entity.Property(e => e.AvailableQty).HasColumnType("decimal(38, 3)");
        entity.Property(e => e.FabricWidthCm).HasColumnType("decimal(7, 2)");
        entity.Property(e => e.NeedMeters).HasColumnType("decimal(18, 3)");
        entity.Property(e => e.PieceLengthCm).HasColumnType("decimal(7, 2)");
        entity.Property(e => e.PieceWidthCm).HasColumnType("decimal(7, 2)");
        entity.Property(e => e.Unit).HasMaxLength(20);
    }
}
