using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>TA-03 / UC35: sổ xe và nhiên liệu — cảnh báo lệch km và xăng vượt định mức (MSG30).</summary>
public class VehicleServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 12);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ISystemSettingReader _settings = Substitute.For<ISystemSettingReader>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<Vehicle> _vehicles = new();
    private readonly List<VehicleTrip> _trips = new();
    private readonly List<DispatchNote> _notes = new();
    private readonly Vehicle _truck = new() { VehicleId = 1, PlateNumber = "29C-12345", VehicleType = "Xe tải", FuelNormPer100Km = 12m, Status = RecordStatus.Active };

    public VehicleServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _settings.GetDecimalAsync(SettingKeys.FuelNormTolerancePercent, Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(15m);
        _vehicles.Add(_truck);
        Setup(_vehicles); Setup(_trips); Setup(_notes);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private VehicleService Service() => new(_uow, _settings, _audit, _clock);

    private VehicleTrip Trip(string status, int start, int? end, DateOnly? date = null, int id = 0)
    {
        var t = new VehicleTrip
        {
            VehicleTripId = id == 0 ? _trips.Count + 1 : id, VehicleId = 1, Vehicle = _truck, TripDate = date ?? Today.AddDays(-1),
            DriverName = "Tài xế", OdometerStart = start, OdometerEnd = end, Status = status,
        };
        _trips.Add(t);
        return t;
    }

    private List<VehicleTrip> CaptureTrips()
    {
        var added = new List<VehicleTrip>();
        _uow.Repository<VehicleTrip>().AddAsync(Arg.Do<VehicleTrip>(t => { t.Vehicle = _truck; t.VehicleTripId = _trips.Count + 1; added.Add(t); _trips.Add(t); }));
        return added;
    }

    private static StartVehicleTripDto Start(int odometer, DateOnly? date = null) =>
        new(1, date ?? Today, " Nguyễn Văn A ", null, "Giao hàng", null, odometer);

    // ---------- Xe ----------

    [Fact]
    public async Task Vehicle_plate_is_normalised_and_must_be_unique()
    {
        var created = await Service().CreateVehicleAsync(new SaveVehicleDto(" 29c-99999 ", " Xe tải nhỏ ", 10m), default);

        created.IsSuccess.Should().BeTrue();
        created.Data!.PlateNumber.Should().Be("29C-99999");

        var duplicate = await Service().CreateVehicleAsync(new SaveVehicleDto("29c-12345", "Xe tải", 12m), default);
        duplicate.ErrorCode.Should().Be(ErrorCodes.VehiclePlateDuplicate);
    }

    [Fact]
    public async Task Update_cannot_take_another_vehicles_plate()
    {
        _vehicles.Add(new Vehicle { VehicleId = 2, PlateNumber = "30A-11111", VehicleType = "Xe con", FuelNormPer100Km = 8m, Status = RecordStatus.Active });

        var clash = await Service().UpdateVehicleAsync(2, new SaveVehicleDto("29C-12345", "Xe con", 8m), default);
        var ok = await Service().UpdateVehicleAsync(1, new SaveVehicleDto("29c-12345", "Xe tải lớn", 13m), default);

        clash.ErrorCode.Should().Be(ErrorCodes.VehiclePlateDuplicate);
        ok.IsSuccess.Should().BeTrue();
        _truck.FuelNormPer100Km.Should().Be(13m);
    }

    [Fact]
    public async Task A_vehicle_is_never_deleted_only_discontinued_and_reactivated()
    {
        (await Service().ChangeVehicleStatusAsync(1, active: false, default)).IsSuccess.Should().BeTrue();
        _truck.Status.Should().Be(RecordStatus.Discontinued);

        (await Service().ChangeVehicleStatusAsync(1, active: true, default)).IsSuccess.Should().BeTrue();
        _truck.Status.Should().Be(RecordStatus.Active);
        (await Service().ChangeVehicleStatusAsync(99, active: true, default)).ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    // ---------- Bắt đầu chuyến: lệch km ----------

    [Fact]
    public async Task First_trip_of_a_vehicle_has_no_km_warning()
    {
        CaptureTrips();

        var result = await Service().StartTripAsync(Start(1000), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.KmGapWarning.Should().BeFalse();
        result.Data.Status.Should().Be(TripStatuses.Started);
        result.Data.DriverName.Should().Be("Nguyễn Văn A");
    }

    [Fact]
    public async Task Start_km_that_continues_the_previous_trip_has_no_warning()
    {
        Trip(TripStatuses.Completed, 1000, 1100);
        CaptureTrips();

        var result = await Service().StartTripAsync(Start(1100), default);

        result.Data!.KmGapWarning.Should().BeFalse();
        result.Message.Should().Be(WarehouseMessages.TripStarted);
    }

    [Fact]
    public async Task Start_km_that_does_not_continue_the_previous_trip_is_saved_with_the_msg30_warning()
    {
        Trip(TripStatuses.Completed, 1000, 1100);
        var added = CaptureTrips();

        var result = await Service().StartTripAsync(Start(1150), default);

        result.IsSuccess.Should().BeTrue();   // cảnh báo, không chặn
        result.Data!.KmGapWarning.Should().BeTrue();
        result.Message.Should().Contain("1100");
        added.Single().KmGapWarning.Should().BeTrue();
    }

    [Fact]
    public async Task Only_a_completed_trip_counts_as_the_previous_trip()
    {
        Trip(TripStatuses.Completed, 1000, 1100, date: Today.AddDays(-3));
        Trip(TripStatuses.Completed, 1100, 1200, date: Today.AddDays(-2));

        CaptureTrips();
        (await Service().StartTripAsync(Start(1200), default)).Data!.KmGapWarning.Should().BeFalse();
    }

    [Fact]
    public async Task A_vehicle_cannot_have_two_open_trips_a_future_date_or_a_duplicate_start()
    {
        Trip(TripStatuses.Started, 1000, null);
        (await Service().StartTripAsync(Start(1100), default)).ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);

        _trips.Clear();
        (await Service().StartTripAsync(Start(1000, Today.AddDays(1)), default)).ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);

        Trip(TripStatuses.Completed, 1000, 1100, date: Today);
        (await Service().StartTripAsync(Start(1000), default)).ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
    }

    [Fact]
    public async Task A_discontinued_vehicle_or_unknown_dispatch_note_is_refused()
    {
        _truck.Status = RecordStatus.Discontinued;
        (await Service().StartTripAsync(Start(1000), default)).ErrorCode.Should().Be(ErrorCodes.VehicleInactive);

        _truck.Status = RecordStatus.Active;
        (await Service().StartTripAsync(Start(1000) with { DispatchNoteId = 55 }, default)).ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    // ---------- Kết thúc chuyến: xăng vượt định mức ----------

    [Fact]
    public async Task Fuel_within_the_norm_plus_tolerance_gives_no_warning()
    {
        var trip = Trip(TripStatuses.Started, 1000, null);

        // Định mức 12 l/100 km × 200 km = 24 lít; ngưỡng cảnh báo = 24 × 1,15 = 27,6 lít
        var result = await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1200, 27.6m, 600000), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.FuelOverNormWarning.Should().BeFalse();
        result.Data.Distance.Should().Be(200);
        result.Data.FuelNormLiters.Should().Be(24m);
        trip.Status.Should().Be(TripStatuses.Completed);
    }

    [Fact]
    public async Task Fuel_above_the_norm_plus_tolerance_is_saved_with_the_msg30_warning()
    {
        var trip = Trip(TripStatuses.Started, 1000, null);

        var result = await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1200, 27.7m, 650000), default);

        result.IsSuccess.Should().BeTrue();
        result.Data!.FuelOverNormWarning.Should().BeTrue();
        result.Message.Should().Contain("24");
    }

    [Fact]
    public async Task The_tolerance_comes_from_the_system_setting()
    {
        _settings.GetDecimalAsync(SettingKeys.FuelNormTolerancePercent, Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(30m);
        var trip = Trip(TripStatuses.Started, 1000, null);

        var result = await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1200, 30m, null), default);

        result.Data!.FuelOverNormWarning.Should().BeFalse();   // ngưỡng 24 × 1,30 = 31,2
    }

    [Fact]
    public async Task A_trip_without_a_fuel_amount_never_triggers_the_fuel_warning()
    {
        var trip = Trip(TripStatuses.Started, 1000, null);

        var result = await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1200, null, null), default);

        result.Data!.FuelOverNormWarning.Should().BeFalse();
    }

    [Fact]
    public async Task End_km_must_be_greater_than_start_km_and_a_completed_trip_cannot_be_completed_again()
    {
        var trip = Trip(TripStatuses.Started, 1000, null);
        (await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1000, 5, null), default))
            .ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);

        (await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1100, 5, null), default)).IsSuccess.Should().BeTrue();
        (await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1200, 5, null), default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    // ---------- Danh sách, quyền ảnh, validator ----------

    [Fact]
    public async Task Trips_can_be_filtered_to_those_with_warnings()
    {
        var ok = Trip(TripStatuses.Completed, 1000, 1100);
        var gap = Trip(TripStatuses.Completed, 1150, 1250); gap.KmGapWarning = true;
        var fuel = Trip(TripStatuses.Completed, 1250, 1350); fuel.FuelOverNormWarning = true;

        var all = (await Service().GetTripsAsync(new VehicleTripQuery(), default)).Data!;
        var warned = (await Service().GetTripsAsync(new VehicleTripQuery { WarningsOnly = true }, default)).Data!;

        all.Items.Should().HaveCount(3);
        warned.Items.Select(t => t.VehicleTripId).Should().BeEquivalentTo(new[] { gap.VehicleTripId, fuel.VehicleTripId });
        ok.KmGapWarning.Should().BeFalse();
    }

    [Theory]
    [InlineData(Roles.Accountant, true, true)]
    [InlineData(Roles.Admin, true, false)]
    [InlineData(Roles.Manager, false, false)]
    [InlineData(Roles.WarehouseKeeper, false, false)]
    public async Task Fuel_receipt_photos_are_for_the_accountant_only(string role, bool canRead, bool canWrite)
    {
        var user = Substitute.For<ICurrentUser>();
        user.Role.Returns(role);
        var trip = Trip(TripStatuses.Started, 1000, null);
        var access = new VehicleTripAttachmentAccess(_uow, user);

        (await access.CanReadAsync(trip.VehicleTripId, default)).Should().Be(canRead);
        (await access.CanWriteAsync(trip.VehicleTripId, default)).Should().Be(canWrite);
    }

    [Fact]
    public void Validators_require_plate_type_norm_driver_and_valid_km()
    {
        var vehicle = new SaveVehicleDtoValidator();
        vehicle.Validate(new SaveVehicleDto("29C-1", "Xe tải", 12m)).IsValid.Should().BeTrue();
        vehicle.Validate(new SaveVehicleDto("", "Xe tải", 12m)).IsValid.Should().BeFalse();
        vehicle.Validate(new SaveVehicleDto("29C-1", "", 12m)).IsValid.Should().BeFalse();
        vehicle.Validate(new SaveVehicleDto("29C-1", "Xe tải", 0m)).IsValid.Should().BeFalse();

        var start = new StartVehicleTripDtoValidator();
        start.Validate(Start(0)).IsValid.Should().BeTrue();
        start.Validate(Start(-1)).IsValid.Should().BeFalse();
        start.Validate(Start(10) with { DriverName = " " }).IsValid.Should().BeFalse();

        var complete = new CompleteVehicleTripDtoValidator();
        complete.Validate(new CompleteVehicleTripDto(100, 5, 100000)).IsValid.Should().BeTrue();
        complete.Validate(new CompleteVehicleTripDto(100, -1, null)).IsValid.Should().BeFalse();
        complete.Validate(new CompleteVehicleTripDto(0, null, null)).IsValid.Should().BeFalse();
    }
}
