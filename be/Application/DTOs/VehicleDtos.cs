using NX_MTD.Application.Common;

namespace NX_MTD.Application.DTOs;

public sealed record VehicleDto(int VehicleId, string PlateNumber, string VehicleType, decimal FuelNormPer100Km, string Status,
    int TripCount, int? LastOdometer);

public sealed record SaveVehicleDto(string PlateNumber, string VehicleType, decimal FuelNormPer100Km);

public sealed record VehicleQuery : PageQuery
{
    public string? Status { get; init; }
}

public sealed record VehicleTripDto(
    int VehicleTripId, int VehicleId, string PlateNumber, DateOnly TripDate, string DriverName, int? DriverEmployeeId,
    string? Purpose, int? DispatchNoteId, string? DispatchCode, int OdometerStart, int? OdometerEnd, int? Distance,
    decimal? FuelLiters, decimal? FuelCost, decimal? FuelNormLiters, string Status, bool KmGapWarning,
    bool FuelOverNormWarning, DateTime RecordedAt);

/// <summary>Bắt đầu chuyến: km đầu không liền với km cuối chuyến trước thì bật cảnh báo MSG30 (vẫn lưu được).</summary>
public sealed record StartVehicleTripDto(int VehicleId, DateOnly TripDate, string? DriverName, int? DriverEmployeeId,
    string? Purpose, int? DispatchNoteId, int OdometerStart);

/// <summary>Kết thúc chuyến: km cuối &gt; km đầu; xăng vượt định mức quá dung sai thì bật cảnh báo MSG30.</summary>
public sealed record CompleteVehicleTripDto(int OdometerEnd, decimal? FuelLiters, decimal? FuelCost);

public sealed record VehicleTripQuery : PageQuery
{
    public int? VehicleId { get; init; }
    public string? Status { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public bool? WarningsOnly { get; init; }
}
