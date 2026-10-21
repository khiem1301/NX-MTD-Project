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
/// MI-19 — UC12 "Tổng hợp chi phí" (CHỈ Admin, GBR-02). CostSummaryService không copy lại logic gộp của
/// MonthlyCostService (MI-12) — gọi thẳng GetByPeriodMonthAsync rồi cộng Payroll.TotalAmount đã duyệt. Bao
/// phủ: propagate lỗi từ MonthlyCostService, gộp fixed/variable theo mã hàng, payroll chưa duyệt → null,
/// grand total.
/// </summary>
public class CostSummaryServiceTests
{
    private const string Month = "2026-10";
    private static readonly DateOnly MonthStart = new(2026, 10, 1);
    private static readonly DateOnly MonthEnd = new(2026, 10, 31);

    private readonly IMonthlyCostService _monthlyCosts = Substitute.For<IMonthlyCostService>();
    private readonly IPayrollRepository _payrolls = Substitute.For<IPayrollRepository>();
    private readonly ICostSummaryExcelWriter _writer = Substitute.For<ICostSummaryExcelWriter>();
    private readonly CostSummaryService _sut;

    public CostSummaryServiceTests()
    {
        _sut = new CostSummaryService(_monthlyCosts, _payrolls, _writer, NullLogger<CostSummaryService>.Instance);
    }

    private static AccountingPeriod Period(int id = 1) =>
        new() { PeriodId = id, PeriodMonth = Month, StartDate = MonthStart, EndDate = MonthEnd, Status = RecordStatus.Open };

    private static Payroll ApprovedPayroll(int periodId, decimal total) =>
        new() { PeriodId = periodId, Status = PayrollStatus.Approved, TotalAmount = total, RowVer = [] };

    private static MealsReconciliationDto NoReconciliation() => new(0m, null, false);

    private static MonthlyCostSummaryDto CostSummary(int periodId, IReadOnlyList<MonthlyCostItemDto> items, bool isLocked = false) =>
        new(periodId, Month, isLocked, items.Sum(i => i.Amount), items, NoReconciliation());

    [Fact]
    public async Task GetAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.GetAsync("2026-13", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostPeriodMonthInvalid);
    }

    [Fact]
    public async Task GetAsync_MonthlyCostServiceFails_PropagatesSameFailure()
    {
        _monthlyCosts.GetByPeriodMonthAsync(Month, Arg.Any<CancellationToken>())
            .Returns(Result<MonthlyCostSummaryDto>.Failure(
                ErrorCodes.MonthlyCostNotFound, Messages.MonthlyCostNotFound, ResultKind.NotFound));

        var result = await _sut.GetAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostNotFound);
    }

    [Fact]
    public async Task GetAsync_Valid_GroupsFixedAndVariableByProductAndAddsApprovedPayroll()
    {
        var period = Period();
        var items = new List<MonthlyCostItemDto>
        {
            new(1, "Meals", "Fixed", null, null, null, 20_000_000m, null, null, DateTime.UtcNow),
            new(2, "Fuel", "Fixed", null, null, null, 5_000_000m, null, null, DateTime.UtcNow),
            new(3, "MaterialsBought", "Variable", 1, "SP001", "Áo thun nam", 3_000_000m, 100, null, DateTime.UtcNow),
            new(4, "Scrap", "Variable", 1, "SP001", "Áo thun nam", 500_000m, null, null, DateTime.UtcNow),
            new(5, "MaterialsBought", "Variable", 2, "SP002", "Quần jean", 7_000_000m, 50, null, DateTime.UtcNow),
        };
        _monthlyCosts.GetByPeriodMonthAsync(Month, Arg.Any<CancellationToken>())
            .Returns(Result<MonthlyCostSummaryDto>.Success(CostSummary(period.PeriodId, items)));
        _payrolls.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _payrolls.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>())
            .Returns(ApprovedPayroll(period.PeriodId, 50_000_000m));

        var result = await _sut.GetAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var data = result.Data!;
        data.FixedCostTotal.Should().Be(25_000_000m);
        data.FixedCosts.Should().HaveCount(2);
        data.VariableCostTotal.Should().Be(10_500_000m);
        data.VariableCostsByProduct.Should().HaveCount(2);
        data.VariableCostsByProduct.Single(p => p.ProductCode == "SP001").Amount.Should().Be(3_500_000m);
        data.VariableCostsByProduct.Single(p => p.ProductCode == "SP002").Amount.Should().Be(7_000_000m);
        data.MonthlyCostTotal.Should().Be(35_500_000m);
        data.PayrollApproved.Should().BeTrue();
        data.PayrollTotal.Should().Be(50_000_000m);
        data.GrandTotal.Should().Be(85_500_000m);
    }

    [Fact]
    public async Task GetAsync_PayrollNotApproved_PayrollTotalIsNull()
    {
        var period = Period();
        var items = new List<MonthlyCostItemDto>
        {
            new(1, "Fuel", "Fixed", null, null, null, 5_000_000m, null, null, DateTime.UtcNow),
        };
        _monthlyCosts.GetByPeriodMonthAsync(Month, Arg.Any<CancellationToken>())
            .Returns(Result<MonthlyCostSummaryDto>.Success(CostSummary(period.PeriodId, items)));
        _payrolls.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _payrolls.FindPayrollByPeriodIdAsync(period.PeriodId, false, Arg.Any<CancellationToken>())
            .Returns(new Payroll { PeriodId = period.PeriodId, Status = PayrollStatus.Draft, TotalAmount = 0m, RowVer = [] });

        var result = await _sut.GetAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.PayrollApproved.Should().BeFalse();
        result.Data.PayrollTotal.Should().BeNull();
        result.Data.GrandTotal.Should().Be(5_000_000m);
    }

    [Fact]
    public async Task GetAsync_NoPeriodForPayroll_PayrollTotalIsNull()
    {
        var period = Period();
        var items = new List<MonthlyCostItemDto>
        {
            new(1, "Fuel", "Fixed", null, null, null, 1_000_000m, null, null, DateTime.UtcNow),
        };
        _monthlyCosts.GetByPeriodMonthAsync(Month, Arg.Any<CancellationToken>())
            .Returns(Result<MonthlyCostSummaryDto>.Success(CostSummary(period.PeriodId, items)));
        _payrolls.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        var result = await _sut.GetAsync(Month, CancellationToken.None);

        result.Data!.PayrollApproved.Should().BeFalse();
        result.Data.PayrollTotal.Should().BeNull();
    }

    [Fact]
    public async Task ExportAsync_Valid_BuildsFileFromWriter()
    {
        var period = Period();
        var items = new List<MonthlyCostItemDto>
        {
            new(1, "Fuel", "Fixed", null, null, null, 1_000_000m, null, null, DateTime.UtcNow),
        };
        _monthlyCosts.GetByPeriodMonthAsync(Month, Arg.Any<CancellationToken>())
            .Returns(Result<MonthlyCostSummaryDto>.Success(CostSummary(period.PeriodId, items)));
        _payrolls.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);
        _writer.WriteCostSummarySheet(Arg.Any<CostSummaryDto>()).Returns(new byte[] { 9, 8, 7 });

        var result = await _sut.ExportAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.FileName.Should().Be($"TongHopChiPhi_{Month}.xlsx");
        result.Data.Content.Should().Equal(9, 8, 7);
    }

    [Fact]
    public async Task ExportAsync_MonthlyCostServiceFails_PropagatesFailure()
    {
        _monthlyCosts.GetByPeriodMonthAsync(Month, Arg.Any<CancellationToken>())
            .Returns(Result<MonthlyCostSummaryDto>.Failure(
                ErrorCodes.MonthlyCostNotFound, Messages.MonthlyCostNotFound, ResultKind.NotFound));

        var result = await _sut.ExportAsync(Month, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.MonthlyCostNotFound);
    }
}
