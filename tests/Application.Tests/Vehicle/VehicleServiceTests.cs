using FluentAssertions;
using FluentAssertions.Execution;
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
/// UTCID568–UTCID587: VehicleService.CompleteTripAsync (FuelCost, ảnh hóa đơn IsCurrent, FuelOverNormWarning).
/// Assert khóa theo sheet VehicleService trong UNIT_TEST_CASES_REBASELINE_v3.1 — không nới Expected cho Actual lệch spec.
/// </summary>
public class VehicleServiceTests
{
    private const int T1 = 1;
    private const int T2 = 2;
    private const int T3 = 3;
    private const int StartKm = 10_000;
    private const int EndKm = 10_150;
    private const decimal TolerancePercent = 10m;

    private static readonly DateTime Now = new(2026, 11, 24, 8, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 24);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ISystemSettingReader _settings = Substitute.For<ISystemSettingReader>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();

    private readonly Vehicle _vehicle = new()
    {
        VehicleId = 1,
        PlateNumber = "51C-123.45",
        VehicleType = "Xe tải",
        FuelNormPer100Km = 12m,
        Status = RecordStatus.Active,
    };

    private readonly List<VehicleTrip> _trips = [];
    private readonly List<Attachment> _attachments = [];
    private readonly List<AuditEntry> _audits = [];

    public VehicleServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _settings.GetDecimalAsync(SettingKeys.FuelNormTolerancePercent, Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(TolerancePercent);
        _uow.SaveChangesAsync().Returns(1);
        _locks.AcquireAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _audit.When(a => a.Add(Arg.Any<AuditEntry>())).Do(ci => _audits.Add(ci.Arg<AuditEntry>()));

        var trips = Substitute.For<IGenericRepository<VehicleTrip>>();
        trips.Query().Returns(_ => _trips.AsTestAsyncQueryable());
        _uow.Repository<VehicleTrip>().Returns(trips);

        var attachments = Substitute.For<IGenericRepository<Attachment>>();
        attachments.Query().Returns(_ => _attachments.AsTestAsyncQueryable());
        _uow.Repository<Attachment>().Returns(attachments);

        AddTrip(T1, TripStatuses.Started);
    }

    private VehicleService Sut() => new(_uow, _settings, _audit, _clock, _locks);

    private Task<Result<VehicleTripDto>> Complete(int tripId, CompleteVehicleTripDto dto) =>
        Sut().CompleteTripAsync(tripId, dto, CancellationToken.None);

    private static CompleteVehicleTripDto Dto(int odometerEnd, decimal? liters, decimal? cost) =>
        new(odometerEnd, liters, cost);

    [Fact]
    public async Task UTCID568_CompleteTripAsync_WithinNorm_CompletesWithoutWarning()
    {
        // Arrange — T1 Started, đầu 10.000; 150 km × 12L/100km = 18L, dung sai 10% → ngưỡng 19,8L
        var trip = Trip(T1);

        // Act
        var result = await Complete(T1, Dto(EndKm, 15m, null));

        // Assert — Success; Completed; không vượt ngưỡng; Audit + SaveChanges
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Message.Should().Be(WarehouseMessages.TripCompleted);
            result.Data!.Status.Should().Be(TripStatuses.Completed);
            result.Data.OdometerEnd.Should().Be(EndKm);
            result.Data.FuelLiters.Should().Be(15m);
            result.Data.FuelCost.Should().BeNull();
            result.Data.FuelOverNormWarning.Should().BeFalse();
            trip.Status.Should().Be(TripStatuses.Completed);
            trip.OdometerEnd.Should().Be(EndKm);
            trip.FuelLiters.Should().Be(15m);
            trip.FuelOverNormWarning.Should().BeFalse();
            _audits.Should().ContainSingle();
            _audits[0].TableName.Should().Be(nameof(VehicleTrip));
            _audits[0].Action.Should().Be(AuditActions.Update);
            _audits[0].RecordId.Should().Be(T1);
            await _uow.Received(1).SaveChangesAsync();
            await _settings.Received(1).GetDecimalAsync(
                SettingKeys.FuelNormTolerancePercent, Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task UTCID569_CompleteTripAsync_WithCurrentReceipt_SavesFuelCost()
    {
        // Arrange — có FuelReceipt IsCurrent
        AddReceipt(T1, current: true);
        var trip = Trip(T1);

        // Act
        var result = await Complete(T1, Dto(EndKm, 15m, 500_000m));

        // Assert — FuelCost 500.000 được lưu
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Data!.FuelCost.Should().Be(500_000m);
            result.Data.Status.Should().Be(TripStatuses.Completed);
            trip.FuelCost.Should().Be(500_000m);
            trip.Status.Should().Be(TripStatuses.Completed);
            await _uow.Received(1).SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UTCID570_CompleteTripAsync_OdometerEndEqualStart_Validation()
    {
        // Arrange — Km cuối = km đầu
        var trip = Trip(T1);
        var message = string.Format(WarehouseMessages.OdometerEndInvalid, StartKm);

        // Act
        var result = await Complete(T1, Dto(StartKm, 15m, null));

        // Assert — Failure Validation, field odometerEnd (<=)
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.Validation);
            result.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
            result.Message.Should().Be(message);
            result.Errors.Should().ContainKey("odometerEnd");
            result.Errors!["odometerEnd"].Should().Equal(message);
            trip.Status.Should().Be(TripStatuses.Started);
            trip.OdometerEnd.Should().BeNull();
            _audits.Should().BeEmpty();
            await _uow.DidNotReceive().SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UTCID571_CompleteTripAsync_OdometerEndJustAboveStart_Succeeds()
    {
        // Arrange — Km cuối = km đầu + 1
        var trip = Trip(T1);

        // Act
        var result = await Complete(T1, Dto(StartKm + 1, null, null));

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Data!.Status.Should().Be(TripStatuses.Completed);
            result.Data.OdometerEnd.Should().Be(StartKm + 1);
            trip.Status.Should().Be(TripStatuses.Completed);
            trip.OdometerEnd.Should().Be(StartKm + 1);
        }
    }

    [Fact]
    public async Task UTCID572_CompleteTripAsync_FuelAtTolerance_NoWarning()
    {
        // Arrange — đúng ngưỡng 19,8L (18L × 1,10)
        var trip = Trip(T1);

        // Act
        var result = await Complete(T1, Dto(EndKm, 19.8m, null));

        // Assert — vẫn trong dung sai, không bật cảnh báo
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Message.Should().Be(WarehouseMessages.TripCompleted);
            result.Data!.FuelLiters.Should().Be(19.8m);
            result.Data.FuelOverNormWarning.Should().BeFalse();
            trip.FuelOverNormWarning.Should().BeFalse();
            trip.Status.Should().Be(TripStatuses.Completed);
        }
    }

    [Fact]
    public async Task UTCID573_CompleteTripAsync_FuelJustOverTolerance_WarningStillSaves()
    {
        // Arrange — 19,81L vượt ngưỡng 19,8L
        var trip = Trip(T1);
        var norm = 12m * (EndKm - StartKm) / 100m;
        var message = string.Format(WarehouseMessages.FuelOverNormWarning, Math.Round(norm, 1));

        // Act
        var result = await Complete(T1, Dto(EndKm, 19.81m, null));

        // Assert — Success vẫn lưu; FuelOverNormWarning = true
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Message.Should().Be(message);
            result.Data!.Status.Should().Be(TripStatuses.Completed);
            result.Data.FuelLiters.Should().Be(19.81m);
            result.Data.FuelOverNormWarning.Should().BeTrue();
            trip.FuelLiters.Should().Be(19.81m);
            trip.FuelOverNormWarning.Should().BeTrue();
            trip.Status.Should().Be(TripStatuses.Completed);
            await _uow.Received(1).SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UTCID574_CompleteTripAsync_NoFuelAndNoReceipt_Succeeds()
    {
        // Arrange — không attachment, không lít, không tiền
        var trip = Trip(T1);

        // Act
        var result = await Complete(T1, Dto(EndKm, null, null));

        // Assert — không bắt ảnh
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Data!.Status.Should().Be(TripStatuses.Completed);
            result.Data.FuelLiters.Should().BeNull();
            result.Data.FuelCost.Should().BeNull();
            result.Data.FuelOverNormWarning.Should().BeFalse();
            trip.FuelLiters.Should().BeNull();
            trip.FuelCost.Should().BeNull();
            _attachments.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID575_CompleteTripAsync_LitersWithoutCost_DoesNotRequireReceipt()
    {
        // Arrange — có lít, không tiền, không ảnh. Chỉ bắt ảnh khi FuelCost ≠ null.
        var trip = Trip(T1);

        // Act
        var result = await Complete(T1, Dto(EndKm, 20m, null));

        // Assert — Success, không FuelReceiptRequired
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Kind.Should().Be(ResultKind.Ok);
            result.Data!.Status.Should().Be(TripStatuses.Completed);
            result.Data.FuelLiters.Should().Be(20m);
            result.Data.FuelCost.Should().BeNull();
            trip.Status.Should().Be(TripStatuses.Completed);
            trip.FuelLiters.Should().Be(20m);
            trip.FuelCost.Should().BeNull();
            _attachments.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task UTCID576_CompleteTripAsync_FuelCostMin_Succeeds()
    {
        // Arrange — biên dưới validator: 1 đồng, có ảnh hiện hành
        AddReceipt(T1, current: true);
        var dto = Dto(EndKm, null, 1m);
        var trip = Trip(T1);

        // Act
        var validation = new CompleteVehicleTripDtoValidator().Validate(dto);
        var result = await Complete(T1, dto);

        // Assert
        using (new AssertionScope())
        {
            validation.IsValid.Should().BeTrue();
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Data!.FuelCost.Should().Be(1m);
            trip.FuelCost.Should().Be(1m);
            trip.Status.Should().Be(TripStatuses.Completed);
        }
    }

    [Fact]
    public async Task UTCID577_CompleteTripAsync_FuelCostMax_Succeeds()
    {
        // Arrange — biên trên validator: 100.000.000
        AddReceipt(T1, current: true);
        var dto = Dto(EndKm, null, 100_000_000m);
        var trip = Trip(T1);

        // Act
        var validation = new CompleteVehicleTripDtoValidator().Validate(dto);
        var result = await Complete(T1, dto);

        // Assert
        using (new AssertionScope())
        {
            validation.IsValid.Should().BeTrue();
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Data!.FuelCost.Should().Be(100_000_000m);
            trip.FuelCost.Should().Be(100_000_000m);
            trip.Status.Should().Be(TripStatuses.Completed);
        }
    }

    [Fact]
    public void UTCID578_CompleteTripAsync_FuelCostAboveMax_Validation()
    {
        // Arrange — 100.000.001. Validator ngoài service (ValidationFilter), không nằm trong CompleteTripAsync.
        AddReceipt(T1, current: true);
        var dto = Dto(EndKm, null, 100_000_001m);

        // Act
        var validation = new CompleteVehicleTripDtoValidator().Validate(dto);

        // Assert — Failure Validation
        using (new AssertionScope())
        {
            validation.IsValid.Should().BeFalse();
            validation.Errors.Should().Contain(e =>
                e.PropertyName == nameof(CompleteVehicleTripDto.FuelCost)
                && e.ErrorMessage == WarehouseMessages.FuelCostInvalid);
        }
    }

    [Fact]
    public void UTCID579_CompleteTripAsync_FuelCostNotPositive_Validation()
    {
        // Arrange — 0 hoặc -1. FuelCost phải là số nguyên dương; validator ngoài service.
        AddReceipt(T1, current: true);

        // Act + Assert
        using (new AssertionScope())
        {
            foreach (var cost in new decimal[] { 0m, -1m })
            {
                var validation = new CompleteVehicleTripDtoValidator().Validate(Dto(EndKm, null, cost));
                validation.IsValid.Should().BeFalse($"FuelCost={cost} phải bị từ chối");
                validation.Errors.Should().Contain(e =>
                    e.PropertyName == nameof(CompleteVehicleTripDto.FuelCost)
                    && e.ErrorMessage == WarehouseMessages.FuelCostInvalid);
            }
        }
    }

    [Fact]
    public async Task UTCID580_CompleteTripAsync_FuelCostWithoutReceipt_FuelReceiptRequired()
    {
        // Arrange — có tiền, không có ảnh hóa đơn
        var trip = Trip(T1);

        // Act
        var result = await Complete(T1, Dto(EndKm, 15m, 500_000m));

        // Assert — field fuelCost, FuelReceiptRequired; chuyến vẫn Started
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.Validation);
            result.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
            result.Message.Should().Be(WarehouseMessages.FuelReceiptRequired);
            result.Errors.Should().ContainKey("fuelCost");
            result.Errors!["fuelCost"].Should().Equal(WarehouseMessages.FuelReceiptRequired);
            trip.Status.Should().Be(TripStatuses.Started);
            trip.FuelCost.Should().BeNull();
            trip.OdometerEnd.Should().BeNull();
            _audits.Should().BeEmpty();
            await _uow.DidNotReceive().SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UTCID581_CompleteTripAsync_ReplacedReceipt_FuelReceiptRequired()
    {
        // Arrange — có FuelReceipt nhưng đã thay (IsCurrent = false)
        AddReceipt(T1, current: false);
        var trip = Trip(T1);

        // Act
        var result = await Complete(T1, Dto(EndKm, 15m, 500_000m));

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.Validation);
            result.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
            result.Message.Should().Be(WarehouseMessages.FuelReceiptRequired);
            result.Errors.Should().ContainKey("fuelCost");
            result.Errors!["fuelCost"].Should().Equal(WarehouseMessages.FuelReceiptRequired);
            trip.Status.Should().Be(TripStatuses.Started);
            trip.FuelCost.Should().BeNull();
            await _uow.DidNotReceive().SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UTCID582_CompleteTripAsync_OdometerEndBelowStart_Validation()
    {
        // Arrange — Km cuối 9.990 < km đầu 10.000
        var trip = Trip(T1);
        var message = string.Format(WarehouseMessages.OdometerEndInvalid, StartKm);

        // Act
        var result = await Complete(T1, Dto(9_990, null, null));

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.Validation);
            result.ErrorCode.Should().Be(ErrorCodes.VehicleTripInvalid);
            result.Errors.Should().ContainKey("odometerEnd");
            result.Errors!["odometerEnd"].Should().Equal(message);
            trip.Status.Should().Be(TripStatuses.Started);
            trip.OdometerEnd.Should().BeNull();
            await _uow.DidNotReceive().SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UTCID583_CompleteTripAsync_UnknownTrip_NotFound()
    {
        // Act
        var result = await Complete(999, Dto(EndKm, 15m, null));

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.NotFound);
            result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
            result.Message.Should().Be(WarehouseMessages.NotFound);
            Trip(T1).Status.Should().Be(TripStatuses.Started);
            _audits.Should().BeEmpty();
            await _uow.DidNotReceive().SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UTCID584_CompleteTripAsync_AlreadyCompleted_InvalidStatus()
    {
        // Arrange — T2 đã Completed
        var trip = AddTrip(T2, TripStatuses.Completed);
        trip.OdometerEnd = EndKm;

        // Act
        var result = await Complete(T2, Dto(EndKm + 10, 15m, null));

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
            result.Message.Should().Be(WarehouseMessages.InvalidStatus);
            trip.Status.Should().Be(TripStatuses.Completed);
            _audits.Should().BeEmpty();
            await _uow.DidNotReceive().SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UTCID585_CompleteTripAsync_NotStarted_InvalidStatus()
    {
        // Arrange — T3 chưa Start (Planned)
        var trip = AddTrip(T3, "PLANNED");

        // Act
        var result = await Complete(T3, Dto(EndKm, 15m, null));

        // Assert
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeFalse();
            result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
            result.Message.Should().Be(WarehouseMessages.InvalidStatus);
            trip.Status.Should().Be("PLANNED");
            _audits.Should().BeEmpty();
            await _uow.DidNotReceive().SaveChangesAsync();
        }
    }

    [Fact]
    public async Task UTCID586_CompleteTripAsync_AuditAfter_ContainsFuelCost()
    {
        // Arrange — có ảnh hiện hành, FuelCost 500.000
        AddReceipt(T1, current: true);

        // Act
        var result = await Complete(T1, Dto(EndKm, 15m, 500_000m));

        // Assert — bản ghi Audit "after" chứa FuelCost. ⚠ Audit after không ghi FuelCost (rủi ro #5).
        using (new AssertionScope())
        {
            result.IsSuccess.Should().BeTrue(result.Message);
            result.Data!.FuelCost.Should().Be(500_000m);
            var after = _audits.Should().ContainSingle().Subject.NewValues;
            after.Should().NotBeNull();
            var names = PropertyNames(after);
            names.Should().Contain("FuelCost");
            if (names.Contains("FuelCost"))
                after!.GetType().GetProperty("FuelCost")!.GetValue(after).Should().Be(500_000m);
        }
    }

    [Fact]
    public async Task UTCID587_CompleteTripAsync_ConcurrentRequests_SecondFails()
    {
        // Arrange — hai request cùng đọc T1 Started. Complete không AcquireAsync (rủi ro #6),
        // nên request sau không thấy trạng thái request trước đã ghi.
        var repo = Substitute.For<IGenericRepository<VehicleTrip>>();
        repo.Query().Returns(_ => new List<VehicleTrip> { NewStarted(T1) }.AsTestAsyncQueryable());
        _uow.Repository<VehicleTrip>().Returns(repo);
        var dto = Dto(EndKm, 15m, null);

        // Act
        var first = await Complete(T1, dto);
        var second = await Complete(T1, dto);

        // Assert — request thứ 2 Failure (InvalidStatus / Concurrency)
        using (new AssertionScope())
        {
            first.IsSuccess.Should().BeTrue(first.Message);
            second.IsSuccess.Should().BeFalse();
            second.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        }
    }

    private VehicleTrip Trip(int id) => _trips.Single(t => t.VehicleTripId == id);

    private VehicleTrip AddTrip(int id, string status)
    {
        var trip = NewStarted(id);
        trip.Status = status;
        _trips.Add(trip);
        return trip;
    }

    private VehicleTrip NewStarted(int id) => new()
    {
        VehicleTripId = id,
        VehicleId = _vehicle.VehicleId,
        Vehicle = _vehicle,
        TripDate = Today,
        DriverName = "Tài xế",
        OdometerStart = StartKm,
        Status = TripStatuses.Started,
        RecordedAt = Now,
    };

    private void AddReceipt(int tripId, bool current) => _attachments.Add(new Attachment
    {
        AttachmentId = _attachments.Count + 1,
        OwnerType = AttachmentOwnerTypes.VehicleTrip,
        OwnerId = tripId,
        Purpose = AttachmentPurposes.FuelReceipt,
        FileUrl = "fuel.jpg",
        FileName = "fuel.jpg",
        IsCurrent = current,
        UploadedAt = Now,
    });

    private static IReadOnlyCollection<string> PropertyNames(object? value) =>
        value?.GetType().GetProperties().Select(p => p.Name).ToArray() ?? [];
}
