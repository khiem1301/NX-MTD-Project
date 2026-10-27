using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC35 (TA-03): Kế toán quản lý xe và ghi sổ chuyến. Bắt đầu chuyến với km đầu không liền với km cuối chuyến trước →
/// <c>KmGapWarning</c>; kết thúc chuyến với xăng vượt định mức × (1 + dung sai) → <c>FuelOverNormWarning</c> (MSG30).
/// Cảnh báo chỉ báo cho Kế toán, vẫn lưu được số liệu.
/// </summary>
public sealed class VehicleService : IVehicleService
{
    private const decimal DefaultTolerancePercent = 15;

    private readonly IUnitOfWork _uow;
    private readonly ISystemSettingReader _settings;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;

    public VehicleService(IUnitOfWork uow, ISystemSettingReader settings, IAuditLogService audit, IClock clock)
    {
        _uow = uow; _settings = settings; _audit = audit; _clock = clock;
    }

    private IQueryable<Vehicle> Vehicles => _uow.Repository<Vehicle>().Query();
    private IQueryable<VehicleTrip> Trips => _uow.Repository<VehicleTrip>().Query();

    // ---------- Xe ----------

    public async Task<Result<PagedResult<VehicleDto>>> GetVehiclesAsync(VehicleQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Vehicles.AsNoTracking()
            .Where(v => string.IsNullOrEmpty(query.Status) || v.Status == query.Status)
            .Where(v => string.IsNullOrEmpty(keyword) || v.PlateNumber.Contains(keyword) || v.VehicleType.Contains(keyword))
            .OrderBy(v => v.PlateNumber)
            .Select(v => new VehicleDto(v.VehicleId, v.PlateNumber, v.VehicleType, v.FuelNormPer100Km, v.Status,
                v.VehicleTrips.Count, v.VehicleTrips.Max(t => (int?)(t.OdometerEnd ?? t.OdometerStart))))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<VehicleDto>>.Success(page);
    }

    public async Task<Result<VehicleDto>> CreateVehicleAsync(SaveVehicleDto dto, CancellationToken ct)
    {
        var plate = Normalize(dto.PlateNumber);
        if (await Vehicles.AnyAsync(v => v.PlateNumber == plate, ct))
            return Result<VehicleDto>.Failure(ErrorCodes.VehiclePlateDuplicate, WarehouseMessages.VehiclePlateDuplicate);

        var vehicle = new Vehicle
        {
            PlateNumber = plate, VehicleType = dto.VehicleType.Trim(), FuelNormPer100Km = dto.FuelNormPer100Km,
            Status = RecordStatus.Active,
        };
        await _uow.Repository<Vehicle>().AddAsync(vehicle);
        await _uow.SaveChangesAsync();
        _audit.Add(new AuditEntry(nameof(Vehicle), vehicle.VehicleId, AuditActions.Insert, null, new { vehicle.PlateNumber, vehicle.FuelNormPer100Km }));
        await _uow.SaveChangesAsync();
        return Result<VehicleDto>.Success(ToDto(vehicle, 0, null), WarehouseMessages.VehicleSaved);
    }

    public async Task<Result<VehicleDto>> UpdateVehicleAsync(int vehicleId, SaveVehicleDto dto, CancellationToken ct)
    {
        var vehicle = await Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId, ct);
        if (vehicle is null) return WarehouseFailures.NotFound<VehicleDto>();
        var plate = Normalize(dto.PlateNumber);
        if (await Vehicles.AnyAsync(v => v.PlateNumber == plate && v.VehicleId != vehicleId, ct))
            return Result<VehicleDto>.Failure(ErrorCodes.VehiclePlateDuplicate, WarehouseMessages.VehiclePlateDuplicate);

        var before = new { vehicle.PlateNumber, vehicle.VehicleType, vehicle.FuelNormPer100Km };
        vehicle.PlateNumber = plate;
        vehicle.VehicleType = dto.VehicleType.Trim();
        vehicle.FuelNormPer100Km = dto.FuelNormPer100Km;
        _audit.Add(new AuditEntry(nameof(Vehicle), vehicle.VehicleId, AuditActions.Update, before,
            new { vehicle.PlateNumber, vehicle.VehicleType, vehicle.FuelNormPer100Km }));
        await _uow.SaveChangesAsync();
        return Result<VehicleDto>.Success(ToDto(vehicle, await Trips.CountAsync(t => t.VehicleId == vehicleId, ct), null), WarehouseMessages.VehicleSaved);
    }

    /// <summary>Không xóa cứng xe đã có chuyến (R5): chỉ chuyển ACTIVE ⇄ DISCONTINUED.</summary>
    public async Task<Result> ChangeVehicleStatusAsync(int vehicleId, bool active, CancellationToken ct)
    {
        var vehicle = await Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId, ct);
        if (vehicle is null) return WarehouseFailures.NotFound();
        var to = active ? RecordStatus.Active : RecordStatus.Discontinued;
        if (vehicle.Status == to) return Result.Success(WarehouseMessages.VehicleSaved);

        _audit.Add(new AuditEntry(nameof(Vehicle), vehicle.VehicleId, AuditActions.Update, new { vehicle.Status }, new { Status = to }));
        vehicle.Status = to;
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.VehicleSaved);
    }

    // ---------- Chuyến ----------

    public async Task<Result<PagedResult<VehicleTripDto>>> GetTripsAsync(VehicleTripQuery query, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var rows = await Trips.AsNoTracking().Include(t => t.Vehicle).Include(t => t.DispatchNote).Include(t => t.DriverEmployee)
            .Where(t => query.VehicleId == null || t.VehicleId == query.VehicleId)
            .Where(t => string.IsNullOrEmpty(query.Status) || t.Status == query.Status)
            .Where(t => query.FromDate == null || t.TripDate >= query.FromDate)
            .Where(t => query.ToDate == null || t.TripDate <= query.ToDate)
            .Where(t => query.WarningsOnly != true || t.KmGapWarning || t.FuelOverNormWarning)
            .Where(t => string.IsNullOrEmpty(keyword) || (t.DriverName ?? t.DriverEmployee!.FullName).Contains(keyword) || t.Vehicle.PlateNumber.Contains(keyword))
            .OrderByDescending(t => t.TripDate).ThenByDescending(t => t.VehicleTripId)
            .ToPagedAsync(query, ct);
        return Result<PagedResult<VehicleTripDto>>.Success(new PagedResult<VehicleTripDto>(
            rows.Items.Select(ToDto).ToList(), rows.Page, rows.PageSize, rows.TotalItems));
    }

    public async Task<Result<VehicleTripDto>> GetTripAsync(int tripId, CancellationToken ct)
    {
        var trip = await Trips.AsNoTracking().Include(t => t.Vehicle).Include(t => t.DispatchNote).Include(t => t.DriverEmployee)
            .FirstOrDefaultAsync(t => t.VehicleTripId == tripId, ct);
        return trip is null ? WarehouseFailures.NotFound<VehicleTripDto>() : Result<VehicleTripDto>.Success(ToDto(trip));
    }

    public async Task<Result<VehicleTripDto>> StartTripAsync(StartVehicleTripDto dto, CancellationToken ct)
    {
        var vehicle = await Vehicles.FirstOrDefaultAsync(v => v.VehicleId == dto.VehicleId, ct);
        if (vehicle is null) return WarehouseFailures.NotFound<VehicleTripDto>();
        if (vehicle.Status != RecordStatus.Active)
            return Result<VehicleTripDto>.Failure(ErrorCodes.VehicleInactive, WarehouseMessages.VehicleInactive);
        if (dto.TripDate > _clock.Today)
            return Result<VehicleTripDto>.Failure(ErrorCodes.VehicleTripInvalid, WarehouseMessages.TripDateFuture);
        if (dto.DispatchNoteId is { } noteId && !await _uow.Repository<DispatchNote>().Query().AnyAsync(d => d.DispatchNoteId == noteId, ct))
            return WarehouseFailures.NotFound<VehicleTripDto>();
        if (await Trips.AnyAsync(t => t.VehicleId == dto.VehicleId && t.Status == TripStatuses.Started, ct))
            return Result<VehicleTripDto>.Failure(ErrorCodes.VehicleTripInvalid, WarehouseMessages.TripAlreadyOpen);
        if (await Trips.AnyAsync(t => t.VehicleId == dto.VehicleId && t.TripDate == dto.TripDate && t.OdometerStart == dto.OdometerStart, ct))
            return Result<VehicleTripDto>.Failure(ErrorCodes.VehicleTripInvalid, WarehouseMessages.TripDuplicate);

        // Km đầu phải liền với km cuối chuyến hoàn thành gần nhất của xe (MSG30); lệch vẫn lưu nhưng cảnh báo.
        var lastEnd = await LastOdometerAsync(dto.VehicleId, ct);
        var gap = lastEnd is not null && dto.OdometerStart != lastEnd;

        var trip = new VehicleTrip
        {
            VehicleId = dto.VehicleId, TripDate = dto.TripDate,
            DriverEmployeeId = dto.DriverEmployeeId, DriverName = dto.DriverEmployeeId is null ? dto.DriverName?.Trim() : null,   // CK_VT_Driver: hoặc nhân viên hoặc tên tài xế ngoài
            Purpose = string.IsNullOrWhiteSpace(dto.Purpose) ? null : dto.Purpose.Trim(), DispatchNoteId = dto.DispatchNoteId,
            OdometerStart = dto.OdometerStart, Status = TripStatuses.Started, KmGapWarning = gap, RecordedAt = _clock.Now,
        };
        await _uow.Repository<VehicleTrip>().AddAsync(trip);
        await _uow.SaveChangesAsync();
        _audit.Add(new AuditEntry(nameof(VehicleTrip), trip.VehicleTripId, AuditActions.Insert, null,
            new { trip.VehicleId, trip.TripDate, trip.OdometerStart, trip.KmGapWarning }));
        await _uow.SaveChangesAsync();
        return Result<VehicleTripDto>.Success(await ReloadAsync(trip.VehicleTripId, ct),
            gap ? string.Format(WarehouseMessages.KmGapWarning, lastEnd) : WarehouseMessages.TripStarted);
    }

    public async Task<Result<VehicleTripDto>> CompleteTripAsync(int tripId, CompleteVehicleTripDto dto, CancellationToken ct)
    {
        var trip = await Trips.Include(t => t.Vehicle).Include(t => t.DriverEmployee).FirstOrDefaultAsync(t => t.VehicleTripId == tripId, ct);
        if (trip is null) return WarehouseFailures.NotFound<VehicleTripDto>();
        if (trip.Status != TripStatuses.Started) return WarehouseFailures.InvalidStatus<VehicleTripDto>();
        if (dto.OdometerEnd <= trip.OdometerStart)
            return Result<VehicleTripDto>.Failure(ErrorCodes.VehicleTripInvalid, WarehouseMessages.OdometerEndInvalid);

        trip.OdometerEnd = dto.OdometerEnd;
        trip.FuelLiters = dto.FuelLiters;
        trip.FuelCost = dto.FuelCost;
        trip.Status = TripStatuses.Completed;

        var tolerance = await _settings.GetDecimalAsync(SettingKeys.FuelNormTolerancePercent, DefaultTolerancePercent, ct);
        var norm = NormLiters(trip.Vehicle.FuelNormPer100Km, dto.OdometerEnd - trip.OdometerStart);
        trip.FuelOverNormWarning = dto.FuelLiters is { } liters && liters > norm * (1 + tolerance / 100m);

        _audit.Add(new AuditEntry(nameof(VehicleTrip), trip.VehicleTripId, AuditActions.Update, new { Status = TripStatuses.Started },
            new { trip.Status, trip.OdometerEnd, trip.FuelLiters, trip.FuelOverNormWarning }));
        await _uow.SaveChangesAsync();
        return Result<VehicleTripDto>.Success(ToDto(trip),
            trip.FuelOverNormWarning ? string.Format(WarehouseMessages.FuelOverNormWarning, Math.Round(norm, 1)) : WarehouseMessages.TripCompleted);
    }

    // ---------- Nội bộ ----------

    /// <summary>Km cuối của chuyến đã hoàn thành gần nhất (theo km lớn nhất).</summary>
    private async Task<int?> LastOdometerAsync(int vehicleId, CancellationToken ct) =>
        await Trips.AsNoTracking().Where(t => t.VehicleId == vehicleId && t.Status == TripStatuses.Completed)
            .OrderByDescending(t => t.TripDate).ThenByDescending(t => t.VehicleTripId)
            .Select(t => t.OdometerEnd).FirstOrDefaultAsync(ct);

    private async Task<VehicleTripDto> ReloadAsync(int id, CancellationToken ct) =>
        ToDto(await Trips.AsNoTracking().Include(t => t.Vehicle).Include(t => t.DispatchNote).Include(t => t.DriverEmployee).FirstAsync(t => t.VehicleTripId == id, ct));

    private static string Normalize(string plate) => plate.Trim().ToUpperInvariant();

    private static decimal NormLiters(decimal normPer100Km, int distance) => normPer100Km * distance / 100m;

    private static VehicleDto ToDto(Vehicle v, int tripCount, int? lastOdometer) =>
        new(v.VehicleId, v.PlateNumber, v.VehicleType, v.FuelNormPer100Km, v.Status, tripCount, lastOdometer);

    private static VehicleTripDto ToDto(VehicleTrip t)
    {
        int? distance = t.OdometerEnd is { } end ? end - t.OdometerStart : null;
        return new VehicleTripDto(t.VehicleTripId, t.VehicleId, t.Vehicle.PlateNumber, t.TripDate, t.DriverName ?? t.DriverEmployee?.FullName ?? string.Empty, t.DriverEmployeeId,
            t.Purpose, t.DispatchNoteId, t.DispatchNote?.DispatchCode, t.OdometerStart, t.OdometerEnd, distance, t.FuelLiters, t.FuelCost,
            distance is { } d ? Math.Round(NormLiters(t.Vehicle.FuelNormPer100Km, d), 2) : null, t.Status, t.KmGapWarning,
            t.FuelOverNormWarning, t.RecordedAt);
    }
}

/// <summary>Quyền ảnh phiếu xăng của chuyến xe (OwnerType = VEHICLE_TRIP, mục đích FUEL_RECEIPT): chỉ Kế toán và Admin.</summary>
public sealed class VehicleTripAttachmentAccess : IAttachmentOwnerAccess
{
    private const int MaxPhotos = 5;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;

    public VehicleTripAttachmentAccess(IUnitOfWork uow, ICurrentUser user)
    {
        _uow = uow; _user = user;
    }

    public string OwnerType => AttachmentOwnerTypes.VehicleTrip;

    public async Task<bool> CanReadAsync(int ownerId, CancellationToken ct) =>
        _user.Role is Roles.Accountant or Roles.Admin && await Exists(ownerId, ct);

    public async Task<bool> CanWriteAsync(int ownerId, CancellationToken ct) =>
        _user.Role == Roles.Accountant && await Exists(ownerId, ct);

    public Task<int> MaxFilesAsync(CancellationToken ct) => Task.FromResult(MaxPhotos);

    private Task<bool> Exists(int tripId, CancellationToken ct) =>
        _uow.Repository<VehicleTrip>().Query().AnyAsync(t => t.VehicleTripId == tripId, ct);
}
