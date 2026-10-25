using System;
using System.Collections.Generic;

namespace NX_MTD.Domain.Entities;

/// <summary>
/// Chuyến xe &amp; nhiên liệu
/// </summary>
public partial class VehicleTrip
{
    public int VehicleTripId { get; set; }

    public int VehicleId { get; set; }

    public DateOnly TripDate { get; set; }

    public string? DriverName { get; set; }

    public int? DriverEmployeeId { get; set; }

    public string? Purpose { get; set; }

    public int? DispatchNoteId { get; set; }

    public int OdometerStart { get; set; }

    public int? OdometerEnd { get; set; }

    public decimal? FuelLiters { get; set; }

    public decimal? FuelCost { get; set; }

    public string Status { get; set; } = null!;

    public bool KmGapWarning { get; set; }

    public bool FuelOverNormWarning { get; set; }

    public DateTime RecordedAt { get; set; }

    public virtual DispatchNote? DispatchNote { get; set; }

    public virtual Employee? DriverEmployee { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;
}
