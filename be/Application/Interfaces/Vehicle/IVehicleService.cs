using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC35: Kế toán quản lý xe và ghi sổ chuyến + nhiên liệu (cảnh báo lệch km / xăng vượt định mức, MSG30).</summary>
public interface IVehicleService
{
    Task<Result<PagedResult<VehicleDto>>> GetVehiclesAsync(VehicleQuery query, CancellationToken ct);
    /// <summary>Admin: danh sách xe kèm trạng thái đang chạy hay không (chuyến STARTED), chỉ đọc.</summary>
    Task<Result<PagedResult<VehicleLiveStatusDto>>> GetLiveStatusAsync(VehicleLiveStatusQuery query, CancellationToken ct);
    Task<Result<VehicleDto>> CreateVehicleAsync(SaveVehicleDto dto, CancellationToken ct);
    Task<Result<VehicleDto>> UpdateVehicleAsync(int vehicleId, SaveVehicleDto dto, CancellationToken ct);
    Task<Result> ChangeVehicleStatusAsync(int vehicleId, bool active, CancellationToken ct);

    Task<Result<PagedResult<VehicleTripDto>>> GetTripsAsync(VehicleTripQuery query, CancellationToken ct);
    Task<Result<VehicleTripDto>> GetTripAsync(int tripId, CancellationToken ct);
    Task<Result<VehicleTripDto>> StartTripAsync(StartVehicleTripDto dto, CancellationToken ct);
    Task<Result<VehicleTripDto>> CompleteTripAsync(int tripId, CompleteVehicleTripDto dto, CancellationToken ct);

    /// <summary>DEV-ALL-127: xe đang hoạt động và chưa có chuyến đang chạy (Thủ kho chọn khi xuất kho).</summary>
    Task<Result<IReadOnlyList<AvailableVehicleDto>>> GetAvailableVehiclesAsync(CancellationToken ct);

    /// <summary>
    /// Kiểm tra và thêm chuyến STARTED vào unit of work, chưa lưu — để gọi trong transaction của nghiệp vụ khác
    /// (xuất kho bằng xe công ty, DEV-ALL-127). Cùng kiểm tra với <see cref="StartTripAsync"/>.
    /// </summary>
    Task<Result<PreparedTrip>> PrepareTripAsync(StartVehicleTripDto dto, CancellationToken ct);
}

/// <param name="LastOdometer">Km cuối chuyến hoàn thành gần nhất; lệch với km đầu thì <c>Trip.KmGapWarning</c> = true.</param>
public sealed record PreparedTrip(NX_MTD.Domain.Entities.VehicleTrip Trip, int? LastOdometer);
