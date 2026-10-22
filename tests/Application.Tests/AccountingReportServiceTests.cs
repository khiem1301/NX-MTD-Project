using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// MI-19 — UC36 "Báo cáo kế toán" (CHỈ Kế toán). Bao phủ: validate tháng, gộp công/tăng ca theo nhân viên
/// tách Chủ nhật (IsSunday), đối soát suất ăn với MonthlyCost MEALS, gộp sổ xe theo xe (km/xăng chỉ tính
/// chuyến COMPLETED, cảnh báo đếm trên mọi chuyến trong kỳ — xem docs/CODE_DEVIATIONS.md DEV-MI-19).
/// </summary>
public class AccountingReportServiceTests
{
    private const string Month = "2026-10";
    private static readonly DateOnly MonthStart = new(2026, 10, 1);
    private static readonly DateOnly MonthEnd = new(2026, 10, 31);

    private readonly IAccountingReportRepository _repo = Substitute.For<IAccountingReportRepository>();
    private readonly IAccountingReportExcelWriter _writer = Substitute.For<IAccountingReportExcelWriter>();
    private readonly AccountingReportService _sut;

    public AccountingReportServiceTests()
    {
        _sut = new AccountingReportService(_repo, _writer, NullLogger<AccountingReportService>.Instance);
    }

    private static AccountingPeriod Period(int id = 1) =>
        new() { PeriodId = id, PeriodMonth = Month, StartDate = MonthStart, EndDate = MonthEnd, Status = RecordStatus.Open };

    // ================= Công + tăng ca =================

    [Fact]
    public async Task GetWorkdayOvertimeReportAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.GetWorkdayOvertimeReportAsync("2026-13", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.AccountingReportPeriodMonthInvalid);
    }

    [Fact]
    public async Task GetWorkdayOvertimeReportAsync_Valid_AggregatesPerEmployeeSplitBySunday()
    {
        _repo.GetTimesheetRowsAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>()).Returns(new List<
            (int EmployeeId, string EmployeeCode, string FullName, string? TeamName, decimal WorkdayValue,
                decimal OvertimeHours, bool IsSunday)>
        {
            (1, "NV001", "Nguyễn Văn A", "Tổ may 1", 1m, 1m, false),
            (1, "NV001", "Nguyễn Văn A", "Tổ may 1", 1m, 0.5m, false),
            (1, "NV001", "Nguyễn Văn A", "Tổ may 1", 1m, 2m, true),
            (2, "NV002", "Trần Thị B", "Tổ may 1", 1m, 0m, false),
        });

        var result = await _sut.GetWorkdayOvertimeReportAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var data = result.Data!;
        data.Rows.Should().HaveCount(2);

        var nv001 = data.Rows.Single(r => r.EmployeeCode == "NV001");
        nv001.RegularWorkdays.Should().Be(2m);
        nv001.SundayWorkdays.Should().Be(1m);
        nv001.TotalWorkdays.Should().Be(3m);
        nv001.TotalOvertimeHours.Should().Be(3.5m);

        data.TotalWorkdays.Should().Be(4m);
        data.TotalSundayWorkdays.Should().Be(1m);
        data.TotalOvertimeHours.Should().Be(3.5m);
    }

    [Fact]
    public async Task GetWorkdayOvertimeReportAsync_NoRows_ReturnsNoDataMessage()
    {
        _repo.GetTimesheetRowsAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>()).Returns(new List<
            (int EmployeeId, string EmployeeCode, string FullName, string? TeamName, decimal WorkdayValue,
                decimal OvertimeHours, bool IsSunday)>());

        var result = await _sut.GetWorkdayOvertimeReportAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Rows.Should().BeEmpty();
        result.Message.Should().Be(Messages.NoData);
    }

    // ================= Suất ăn + đối soát bếp =================

    [Fact]
    public async Task GetMealReportAsync_Valid_FlagsDiscrepancyAgainstMonthlyCostMeals()
    {
        _repo.GetMealTotalsAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>())
            .Returns((DaysWithData: 26, TotalMeals: 2600, TotalAmount: 52_000_000m));
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(Period());
        _repo.GetMonthlyCostMealsAmountAsync(1, Arg.Any<CancellationToken>()).Returns(50_000_000m);

        var result = await _sut.GetMealReportAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalMealsServed.Should().Be(2600);
        result.Data.TotalMealAmountSystem.Should().Be(52_000_000m);
        result.Data.MonthlyCostMealsAmount.Should().Be(50_000_000m);
        result.Data.HasDiscrepancy.Should().BeTrue();
    }

    [Fact]
    public async Task GetMealReportAsync_NoAccountingPeriod_MonthlyCostAmountIsNull()
    {
        _repo.GetMealTotalsAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>())
            .Returns((DaysWithData: 10, TotalMeals: 1000, TotalAmount: 20_000_000m));
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        var result = await _sut.GetMealReportAsync(Month, CancellationToken.None);

        result.Data!.MonthlyCostMealsAmount.Should().BeNull();
        result.Data.HasDiscrepancy.Should().BeFalse();
    }

    // ================= Sổ xe + xăng =================

    [Fact]
    public async Task GetVehicleReportAsync_Valid_SumsKmAndFuelOnlyForCompletedButCountsAllWarnings()
    {
        var trips = new List<VehicleTrip>
        {
            new()
            {
                VehicleTripId = 1, VehicleId = 10, TripDate = new DateOnly(2026, 10, 5), DriverName = "A",
                OdometerStart = 1000, OdometerEnd = 1100, FuelLiters = 8m, FuelCost = 200_000m,
                Status = VehicleTripStatus.Completed, KmGapWarning = false, FuelOverNormWarning = false,
            },
            new()
            {
                VehicleTripId = 2, VehicleId = 10, TripDate = new DateOnly(2026, 10, 10), DriverName = "A",
                OdometerStart = 1100, OdometerEnd = 1250, FuelLiters = 12m, FuelCost = 300_000m,
                Status = VehicleTripStatus.Completed, KmGapWarning = true, FuelOverNormWarning = true,
            },
            new()
            {
                VehicleTripId = 3, VehicleId = 10, TripDate = new DateOnly(2026, 10, 20), DriverName = "A",
                OdometerStart = 1250, OdometerEnd = null, FuelLiters = null, FuelCost = null,
                Status = VehicleTripStatus.Started, KmGapWarning = true, FuelOverNormWarning = false,
            },
        };
        _repo.GetVehicleTripsAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>()).Returns(trips);
        _repo.GetVehiclesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, (string PlateNumber, string VehicleType)> { [10] = ("51C-12345", "Xe tải") });

        var result = await _sut.GetVehicleReportAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var row = result.Data!.Vehicles.Single();
        row.PlateNumber.Should().Be("51C-12345");
        row.CompletedTripCount.Should().Be(2);
        row.TotalKm.Should().Be(250);          // (1100-1000) + (1250-1100), chuyến STARTED không tính
        row.TotalFuelLiters.Should().Be(20m);
        row.TotalFuelCost.Should().Be(500_000m);
        row.KmGapWarningCount.Should().Be(2);  // đếm trên mọi chuyến trong kỳ, kể cả STARTED
        row.FuelOverNormWarningCount.Should().Be(1);

        result.Data.TotalKm.Should().Be(250);
        result.Data.TotalKmGapWarnings.Should().Be(2);
    }

    [Fact]
    public async Task GetVehicleReportAsync_NoTrips_ReturnsNoDataMessage()
    {
        _repo.GetVehicleTripsAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>())
            .Returns(new List<VehicleTrip>());
        _repo.GetVehiclesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, (string PlateNumber, string VehicleType)>());

        var result = await _sut.GetVehicleReportAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Vehicles.Should().BeEmpty();
        result.Message.Should().Be(Messages.NoData);
    }

    // ================= Xuất Excel =================

    [Fact]
    public async Task ExportAsync_Valid_BuildsWorkbookFromThreeSections()
    {
        _repo.GetTimesheetRowsAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>()).Returns(new List<
            (int EmployeeId, string EmployeeCode, string FullName, string? TeamName, decimal WorkdayValue,
                decimal OvertimeHours, bool IsSunday)>());
        _repo.GetMealTotalsAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>())
            .Returns((DaysWithData: 0, TotalMeals: 0, TotalAmount: 0m));
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);
        _repo.GetVehicleTripsAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>()).Returns(new List<VehicleTrip>());
        _repo.GetVehiclesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, (string PlateNumber, string VehicleType)>());
        _writer.WriteReportWorkbook(
                Arg.Any<WorkdayOvertimeReportDto>(), Arg.Any<MealReportDto>(), Arg.Any<VehicleReportDto>())
            .Returns(new byte[] { 1, 2, 3 });

        var result = await _sut.ExportAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.FileName.Should().Be($"BaoCaoKeToan_{Month}.xlsx");
        result.Data.Content.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ExportAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.ExportAsync("2026-13", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.AccountingReportPeriodMonthInvalid);
    }
}
