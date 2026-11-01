using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Infrastructure.Data.Configurations;

public sealed class VehicleTripConfiguration : IEntityTypeConfiguration<VehicleTrip>
{
    public void Configure(EntityTypeBuilder<VehicleTrip> entity)
    {
        entity.ToTable("VehicleTrip", tb => tb.HasComment("Chuyến xe & nhiên liệu"));

        entity.HasIndex(e => new { e.VehicleId, e.TripDate, e.OdometerStart }, "UQ_VT").IsUnique();

        entity.Property(e => e.DriverName).HasMaxLength(100);
        entity.Property(e => e.FuelCost).HasColumnType("decimal(18, 2)");
        entity.Property(e => e.FuelLiters).HasColumnType("decimal(8, 2)");
        entity.Property(e => e.Purpose).HasMaxLength(300);
        entity.Property(e => e.RecordedAt)
            .HasPrecision(0)
            .HasDefaultValueSql("([dbo].[fn_VnNow]())");
        entity.Property(e => e.Status)
            .HasMaxLength(10)
            .IsUnicode(false)
            .HasDefaultValue("STARTED");

        entity.HasOne(d => d.DispatchNote).WithMany(p => p.VehicleTrips)
            .HasForeignKey(d => d.DispatchNoteId)
            .HasConstraintName("FK_VT_Dispatch");

        entity.HasOne(d => d.DriverEmployee).WithMany(p => p.VehicleTrips)
            .HasForeignKey(d => d.DriverEmployeeId)
            .HasConstraintName("FK_VT_Driver");

        entity.HasOne(d => d.Vehicle).WithMany(p => p.VehicleTrips)
            .HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_VT_Vehicle");
    }
}
