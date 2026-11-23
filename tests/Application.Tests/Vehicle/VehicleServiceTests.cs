using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// TA-03 / UC35: sổ xe và nhiên liệu (MSG30) + phụ lục DEV-ALL-158/159 (UTCID512–532).
/// </summary>
public class VehicleServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 12);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ISystemSettingReader _settings = Substitute.For<ISystemSettingReader>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();

    private readonly List<Vehicle> _vehicles = new();
    private readonly List<VehicleTrip> _trips = new();
    private readonly List<DispatchNote> _notes = new();
    private readonly List<Attachment> _attachments = new();
    private readonly Vehicle _truck = new() { VehicleId = 1, PlateNumber = "29C-12345", VehicleType = "Xe tải", FuelNormPer100Km = 12m, Status = RecordStatus.Active };

    public VehicleServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _settings.GetDecimalAsync(SettingKeys.FuelNormTolerancePercent, Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(15m);
        _locks.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _vehicles.Add(_truck);
        Setup(_vehicles); Setup(_trips); Setup(_notes); Setup(_attachments);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    /// <summary>Gắn một ảnh hóa đơn xăng đang hiệu lực vào chuyến (DEV-ALL-173).</summary>
    private void AddFuelReceipt(VehicleTrip trip) => _attachments.Add(new Attachment
    {
        OwnerType = AttachmentOwnerTypes.VehicleTrip, OwnerId = trip.VehicleTripId, Purpose = AttachmentPurposes.FuelReceipt, IsCurrent = true,
    });

    private VehicleService Service() => new(_uow, _settings, _audit, _clock, _locks);

    private VehicleTrip Trip(string status, int start, int? end, DateOnly? date = null, int id = 0, int vehicleId = 1)
    {
        var vehicle = _vehicles.FirstOrDefault(v => v.VehicleId == vehicleId) ?? _truck;
        var t = new VehicleTrip
        {
            VehicleTripId = id == 0 ? _trips.Count + 1 : id,
            VehicleId = vehicleId,
            Vehicle = vehicle,
            TripDate = date ?? Today.AddDays(-1),
            DriverName = "Tài xế",
            OdometerStart = start,
            OdometerEnd = end,
            Status = status,
        };
        _trips.Add(t);
        return t;
    }

    private List<VehicleTrip> CaptureTrips()
    {
        var added = new List<VehicleTrip>();
        _uow.Repository<VehicleTrip>().AddAsync(Arg.Do<VehicleTrip>(t =>
        {
            t.Vehicle ??= _vehicles.FirstOrDefault(v => v.VehicleId == t.VehicleId) ?? _truck;
            if (t.VehicleTripId == 0) t.VehicleTripId = _trips.Count + 1;
            added.Add(t);
            _trips.Add(t);
        }));
        return added;
    }

    private static StartVehicleTripDto Start(int odometer, DateOnly? date = null, int vehicleId = 1, string? driver = " Nguyễn Văn A ") =>
        new(vehicleId, date ?? Today, driver, null, "Giao hàng", null, odometer);

    /// <summary>UTCID512 setup: xe 1 có 1 Trip STARTED; xe 2 Active không chuyến mở.</summary>
    private VehicleTrip SeedLivePair()
    {
        var idle = new Vehicle { VehicleId = 2, PlateNumber = "30A-11111", VehicleType = "Xe con", FuelNormPer100Km = 8m, Status = RecordStatus.Active };
        _vehicles.Add(idle);
        var open = Trip(TripStatuses.Started, 1000, null, date: Today);
        _truck.VehicleTrips.Add(open);
        return open;
    }

    // ---------- Admin xem trạng thái chạy ----------

    [Fact]
    public async Task LiveStatus_marks_vehicle_with_started_trip_as_running_and_filters()
    {
        var open = SeedLivePair();
        _truck.VehicleTrips.Add(Trip(TripStatuses.Completed, 900, 1000));

        var all = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery(), default);
        var running = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery { Running = true }, default);
        var notRunning = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery { Running = false }, default);

        all.Data!.Items.Select(v => (v.VehicleId, v.IsRunning)).Should().Equal((1, true), (2, false));
        var row = running.Data!.Items.Should().ContainSingle().Subject;
        (row.OpenTripCount, row.CurrentTripId, row.DriverName, row.OdometerStart).Should().Be((1, open.VehicleTripId, "Tài xế", 1000));
        notRunning.Data!.Items.Should().ContainSingle().Which.VehicleId.Should().Be(2);
    }

    // ---------- Phụ lục A — DEV-ALL-158 / DEV-ALL-159 (UTCID512–532) ----------

    [Fact]
    public async Task UTCID512_GetLiveStatusAsync_All_RunningFirst()
    {
        // Arrange
        SeedLivePair();

        // Act
        var result = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Select(v => (v.VehicleId, v.IsRunning, v.OpenTripCount))
            .Should().Equal((1, true, 1), (2, false, 0));
    }

    [Fact]
    public async Task UTCID513_GetLiveStatusAsync_RunningTrue_OnlyOpen()
    {
        // Arrange
        var open = SeedLivePair();

        // Act
        var result = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery { Running = true }, default);

        // Assert
        var row = result.Data!.Items.Should().ContainSingle().Subject;
        row.VehicleId.Should().Be(1);
        (row.CurrentTripId, row.DriverName, row.OdometerStart).Should().Be((open.VehicleTripId, "Tài xế", 1000));
    }

    [Fact]
    public async Task UTCID514_GetLiveStatusAsync_RunningFalse_OnlyIdle()
    {
        // Arrange
        SeedLivePair();

        // Act
        var result = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery { Running = false }, default);

        // Assert
        result.Data!.Items.Should().ContainSingle().Which.VehicleId.Should().Be(2);
    }

    [Fact]
    public async Task UTCID515_GetLiveStatusAsync_KeywordPlate()
    {
        // Arrange
        _truck.PlateNumber = "51C-123.45";
        _truck.VehicleType = "Xe tải";
        _vehicles.Add(new Vehicle { VehicleId = 2, PlateNumber = "30A-11111", VehicleType = "Xe con", FuelNormPer100Km = 8m, Status = RecordStatus.Active });

        // Act
        var result = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery { Keyword = "51C" }, default);

        // Assert
        result.Data!.Items.Should().ContainSingle().Which.VehicleId.Should().Be(1);
    }

    [Fact]
    public async Task UTCID516_GetLiveStatusAsync_KeywordTrim()
    {
        // Arrange
        _truck.PlateNumber = "51C-123.45";
        _vehicles.Add(new Vehicle { VehicleId = 2, PlateNumber = "30A-11111", VehicleType = "Xe con", FuelNormPer100Km = 8m, Status = RecordStatus.Active });

        // Act
        var result = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery { Keyword = "  51C  " }, default);

        // Assert
        result.Data!.Items.Should().ContainSingle().Which.VehicleId.Should().Be(1);
    }

    [Fact]
    public async Task UTCID517_GetLiveStatusAsync_TwoOpenTrips_CountAndLatest()
    {
        // Arrange — data lệch: 2 Trip STARTED
        var older = Trip(TripStatuses.Started, 900, null, date: Today.AddDays(-1), id: 10);
        var newer = Trip(TripStatuses.Started, 1000, null, date: Today, id: 20);
        _truck.VehicleTrips.Add(older);
        _truck.VehicleTrips.Add(newer);

        // Act
        var result = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery(), default);

        // Assert
        var row = result.Data!.Items.Should().ContainSingle().Subject;
        row.IsRunning.Should().BeTrue();
        row.OpenTripCount.Should().Be(2);
        row.CurrentTripId.Should().Be(newer.VehicleTripId);
    }

    [Fact]
    public async Task UTCID518_GetLiveStatusAsync_EmptyFleet()
    {
        // Arrange
        _vehicles.Clear();

        // Act
        var result = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery(), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task UTCID519_GetLiveStatusAsync_OnlyStartedCounts_LastOdometerFromAll()
    {
        // Arrange
        var completed = Trip(TripStatuses.Completed, 900, 950);
        var open = Trip(TripStatuses.Started, 1000, null, date: Today);
        _truck.VehicleTrips.Add(completed);
        _truck.VehicleTrips.Add(open);

        // Act
        var result = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery { Running = true }, default);

        // Assert
        var row = result.Data!.Items.Should().ContainSingle().Subject;
        row.OpenTripCount.Should().Be(1);
        row.CurrentTripId.Should().Be(open.VehicleTripId);
        row.LastOdometer.Should().Be(1000); // Max(OdometerEnd ?? OdometerStart) trên mọi trip
    }

    [Fact]
    public async Task UTCID520_GetLiveStatusAsync_NullKeywordRunning_NoThrow()
    {
        // Arrange
        SeedLivePair();

        // Act
        var result = await Service().GetLiveStatusAsync(new VehicleLiveStatusQuery { Keyword = null, Running = null }, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task UTCID521_StartTripAsync_AcquiresLock_InTransaction_Audit()
    {
        // Arrange
        CaptureTrips();

        // Act
        var result = await Service().StartTripAsync(Start(1000, driver: "A"), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(TripStatuses.Started);
        await _locks.Received(1).AcquireAsync("vehicle-trip:1", Arg.Any<CancellationToken>());
        await _uow.Received().BeginTransactionAsync();
        await _uow.Received().CommitTransactionAsync();
        _audit.Received().Add(Arg.Is<AuditEntry>(e => e.Action == AuditActions.Insert));
    }

    [Fact]
    public async Task UTCID522_StartTripAsync_ContinuesLastEnd_NoKmGap()
    {
        // Arrange
        Trip(TripStatuses.Completed, 900, 1000);
        CaptureTrips();

        // Act
        var result = await Service().StartTripAsync(Start(1000), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.KmGapWarning.Should().BeFalse();
    }

    [Fact]
    public async Task UTCID523_StartTripAsync_KmGap_WarningTrue()
    {
        // Arrange
        Trip(TripStatuses.Completed, 900, 1000);
        CaptureTrips();

        // Act
        var result = await Service().StartTripAsync(Start(1100), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.KmGapWarning.Should().BeTrue();
        result.Message.Should().Contain("1000");
    }

    [Fact]
    public async Task UTCID524_StartTripAsync_TripDateToday_Ok()
    {
        // Arrange
        CaptureTrips();

        // Act
        var result = await Service().StartTripAsync(Start(1000, Today), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(TripStatuses.Started);
    }

    [Fact]
    public async Task UTCID525_StartTripAsync_AlreadyOpen_Fails_StillAcquiresLock()
    {
        // Arrange
        Trip(TripStatuses.Started, 1000, null);
        CaptureTrips();
        var before = _trips.Count;

        // Act
        var result = await Service().StartTripAsync(Start(1100), default);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
        result.Message.Should().Be(WarehouseMessages.TripAlreadyOpen);
        await _locks.Received(1).AcquireAsync("vehicle-trip:1", Arg.Any<CancellationToken>());
        _trips.Should().HaveCount(before);
    }

    [Fact]
    public async Task UTCID526_StartTripAsync_LockTimeout_NoTrip()
    {
        // Arrange — Acquire ném timeout; service hiện không map sang Result Failure (ném ra ngoài)
        CaptureTrips();
        _locks.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("busy"));

        // Act
        var act = () => Service().StartTripAsync(Start(1000), default);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
        await _uow.DidNotReceive().SaveChangesAsync();
        _trips.Should().BeEmpty();
    }

    [Fact]
    public async Task UTCID527_StartTripAsync_InactiveVehicle()
    {
        // Arrange
        _truck.Status = RecordStatus.Discontinued;
        CaptureTrips();

        // Act
        var result = await Service().StartTripAsync(Start(1000), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.VehicleInactive);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UTCID528_StartTripAsync_VehicleNotFound()
    {
        // Arrange
        CaptureTrips();

        // Act
        var result = await Service().StartTripAsync(Start(1000, vehicleId: 999), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID529_StartTripAsync_TripDateFuture()
    {
        // Arrange
        CaptureTrips();

        // Act
        var result = await Service().StartTripAsync(Start(1000, Today.AddDays(1)), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
        result.Message.Should().Be(WarehouseMessages.TripDateFuture);
    }

    [Fact]
    public async Task UTCID530_StartTripAsync_DispatchNoteMissing()
    {
        // Arrange
        CaptureTrips();

        // Act
        var result = await Service().StartTripAsync(Start(1000) with { DispatchNoteId = 55 }, default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    [Fact]
    public async Task UTCID531_StartTripAsync_DuplicateSameDayOdometer()
    {
        // Arrange
        Trip(TripStatuses.Completed, 1000, 1100, date: Today);
        CaptureTrips();

        // Act
        var result = await Service().StartTripAsync(Start(1000), default);

        // Assert
        result.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
        result.Message.Should().Be(WarehouseMessages.TripDuplicate);
    }

    [Fact]
    public async Task UTCID532_StartTripAsync_SecondStartSameVehicle_TripAlreadyOpen()
    {
        // Arrange — hai lần Start tuần tự; lock no-op
        CaptureTrips();

        // Act
        var first = await Service().StartTripAsync(Start(1000), default);
        var second = await Service().StartTripAsync(Start(1100), default);

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeFalse();
        second.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
        second.Message.Should().Be(WarehouseMessages.TripAlreadyOpen);
    }

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
        AddFuelReceipt(trip);

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
        AddFuelReceipt(trip);

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
        var equal = await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1000, 5, null), default);
        equal.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
        equal.Kind.Should().Be(ResultKind.Validation);
        equal.Errors!["odometerEnd"].Single().Should().Contain("1000");
        (await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(900, 5, null), default))
            .ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
        trip.Status.Should().Be(TripStatuses.Started);
        trip.OdometerEnd.Should().BeNull();

        (await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1100, 5, null), default)).IsSuccess.Should().BeTrue();
        (await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1200, 5, null), default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    // ---------- Tiền xăng bắt buộc có ảnh hóa đơn (DEV-ALL-173) ----------

    [Fact]
    public async Task Fuel_cost_without_a_receipt_photo_is_rejected_and_the_trip_stays_open()
    {
        var trip = Trip(TripStatuses.Started, 1000, null);

        var result = await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1200, null, 100000), default);

        result.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
        result.Kind.Should().Be(ResultKind.Validation);
        result.Errors!["fuelCost"].Single().Should().Be(WarehouseMessages.FuelReceiptRequired);
        trip.Status.Should().Be(TripStatuses.Started);
    }

    [Fact]
    public async Task A_removed_or_other_purpose_photo_does_not_count_as_a_fuel_receipt()
    {
        var trip = Trip(TripStatuses.Started, 1000, null);
        _attachments.Add(new Attachment { OwnerType = AttachmentOwnerTypes.VehicleTrip, OwnerId = trip.VehicleTripId, Purpose = AttachmentPurposes.FuelReceipt, IsCurrent = false });
        _attachments.Add(new Attachment { OwnerType = AttachmentOwnerTypes.VehicleTrip, OwnerId = trip.VehicleTripId, Purpose = AttachmentPurposes.Photo, IsCurrent = true });

        (await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1200, null, 100000), default))
            .ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);

        AddFuelReceipt(trip);
        (await Service().CompleteTripAsync(trip.VehicleTripId, new CompleteVehicleTripDto(1200, null, 100000), default)).IsSuccess.Should().BeTrue();
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
        vehicle.Validate(new SaveVehicleDto("29C-12345", "Xe tải", 12m)).IsValid.Should().BeTrue();
        vehicle.Validate(new SaveVehicleDto("", "Xe tải", 12m)).IsValid.Should().BeFalse();
        vehicle.Validate(new SaveVehicleDto("29C-12345", "", 12m)).IsValid.Should().BeFalse();
        vehicle.Validate(new SaveVehicleDto("29C-12345", "Xe tải", 0m)).IsValid.Should().BeFalse();

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
