using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VwOrderLineProgressConfiguration : IEntityTypeConfiguration<VwOrderLineProgress>
{
    public void Configure(EntityTypeBuilder<VwOrderLineProgress> entity)
    {
        entity
            .HasNoKey()
            .ToView("vw_OrderLineProgress");

        entity.Property(e => e.Color).HasMaxLength(50);
        entity.Property(e => e.DefectRatePct).HasColumnType("decimal(5, 2)");
        entity.Property(e => e.OrderCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.ProductCode)
            .HasMaxLength(30)
            .IsUnicode(false);
        entity.Property(e => e.SizeCode)
            .HasMaxLength(10)
            .IsUnicode(false);
    }
}
