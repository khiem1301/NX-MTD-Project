using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> entity)
    {
        entity.ToTable("Vehicle", tb => tb.HasComment("Xe"));

        entity.HasIndex(e => e.PlateNumber, "UQ_Vehicle_Plate").IsUnique();

        entity.Property(e => e.FuelNormPer100Km).HasColumnType("decimal(6, 2)");
        entity.Property(e => e.PlateNumber)
            .HasMaxLength(20)
            .IsUnicode(false);
        entity.Property(e => e.Status)
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasDefaultValue("ACTIVE");
        entity.Property(e => e.VehicleType).HasMaxLength(50);
    }
}
