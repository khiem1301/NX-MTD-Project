using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC35: Kế toán quản lý xe và ghi sổ chuyến + nhiên liệu (cảnh báo lệch km / xăng vượt định mức, MSG30).</summary>
public interface IVehicleService
{
    Task<Result<PagedResult<VehicleDto>>> GetVehiclesAsync(VehicleQuery query, CancellationToken ct);
    Task<Result<VehicleDto>> CreateVehicleAsync(SaveVehicleDto dto, CancellationToken ct);
    Task<Result<VehicleDto>> UpdateVehicleAsync(int vehicleId, SaveVehicleDto dto, CancellationToken ct);
    Task<Result> ChangeVehicleStatusAsync(int vehicleId, bool active, CancellationToken ct);

    Task<Result<PagedResult<VehicleTripDto>>> GetTripsAsync(VehicleTripQuery query, CancellationToken ct);
    Task<Result<VehicleTripDto>> GetTripAsync(int tripId, CancellationToken ct);
    Task<Result<VehicleTripDto>> StartTripAsync(StartVehicleTripDto dto, CancellationToken ct);
    Task<Result<VehicleTripDto>> CompleteTripAsync(int tripId, CompleteVehicleTripDto dto, CancellationToken ct);
}
