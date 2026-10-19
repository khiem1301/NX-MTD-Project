using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Xe
/// </summary>
public partial class Vehicle
{
    public int VehicleId { get; set; }

    public string PlateNumber { get; set; } = null!;

    public string VehicleType { get; set; } = null!;

    public decimal FuelNormPer100Km { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<VehicleTrip> VehicleTrips { get; set; } = new List<VehicleTrip>();
}
