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
/// MI-13 — Doanh thu (UC27) + Hòa vốn, giá trị thặng dư (UC11). Bao phủ: cổng điều kiện MSG25 (GBR-17),
/// hai công thức phân bổ (OUTPUT/LABOUR_HOURS) trên ≥ 2 mã hàng, trường hợp BreakEvenQty = NULL, IsAtRisk,
/// và đảm bảo DTO doanh thu không bao giờ có CmtUnitPrice (GBR-02).
/// </summary>
public class FinanceServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 9, 0, 0);
    private const string Month = "2026-10";
    private static readonly DateOnly MonthStart = new(2026, 10, 1);
    private static readonly DateOnly MonthEnd = new(2026, 10, 31);

    private readonly IFinanceRepository _repo = Substitute.For<IFinanceRepository>();
    private readonly IFinanceExcelWriter _writer = Substitute.For<IFinanceExcelWriter>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly FinanceService _sut;

    public FinanceServiceTests()
    {
        _clock.Now.Returns(Now);
        _sut = new FinanceService(_repo, _writer, _audit, _clock, NullLogger<FinanceService>.Instance);
    }

    private static AccountingPeriod Period(int id = 1) =>
        new() { PeriodId = id, PeriodMonth = Month, StartDate = MonthStart, EndDate = MonthEnd, Status = RecordStatus.Open };

    private static VwMonthlyRevenue Revenue(int orderId, int productId, int qty, decimal revenue) =>
        new() { PeriodMonth = Month, OrderId = orderId, ProductId = productId, FinishedQty = qty, Revenue = revenue };

    private void SetupReady(AccountingPeriod period, bool payrollApproved = true, bool hasCost = true)
    {
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.IsPayrollApprovedAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(payrollApproved);
        _repo.HasMonthlyCostAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(hasCost);
    }

    private void SetupProducts(params (int Id, string Code, string Name)[] products) =>
        _repo.GetProductsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(products.ToDictionary(p => p.Id, p => (p.Code, p.Name)));

    private void CaptureAddedResults(out Func<List<BreakEvenResult>> getAdded)
    {
        var captured = new List<BreakEvenResult>();
        _repo.When(r => r.AddBreakEvenResults(Arg.Any<IEnumerable<BreakEvenResult>>()))
            .Do(c => captured.AddRange(c.Arg<IEnumerable<BreakEvenResult>>()));
        getAdded = () => captured;
    }

    // ================= Cổng điều kiện (GBR-17, MSG25) =================

    [Fact]
    public async Task CalculateAsync_PeriodNotFound_ReturnsNotReady()
    {
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        var result = await _sut.CalculateAsync(new BreakEvenCalculateDto(Month, "Output"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotReady);
        result.Message.Should().Be(Messages.BreakEvenNotReady);
    }

    [Fact]
    public async Task CalculateAsync_PayrollNotApproved_ReturnsNotReady()
    {
        SetupReady(Period(), payrollApproved: false, hasCost: true);

        var result = await _sut.CalculateAsync(new BreakEvenCalculateDto(Month, "Output"), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotReady);
    }

    [Fact]
    public async Task CalculateAsync_NoMonthlyCost_ReturnsNotReady()
    {
        SetupReady(Period(), payrollApproved: true, hasCost: false);

        var result = await _sut.CalculateAsync(new BreakEvenCalculateDto(Month, "Output"), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotReady);
    }

    [Fact]
    public async Task CalculateAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.CalculateAsync(new BreakEvenCalculateDto("2026-13", "Output"), CancellationToken.None);

        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenPeriodMonthInvalid);
    }

    [Fact]
    public async Task CalculateAsync_InvalidAllocationBasis_ReturnsValidationFailure()
    {
        var result = await _sut.CalculateAsync(new BreakEvenCalculateDto(Month, "SOMETHING_ELSE"), CancellationToken.None);

        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenInvalidAllocationBasis);
    }

    [Fact]
    public async Task CalculateAsync_NoRevenueRows_ReturnsNoOutputFailure()
    {
        var period = Period();
        SetupReady(period);
        _repo.GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<VwMonthlyRevenue>());

        var result = await _sut.CalculateAsync(new BreakEvenCalculateDto(Month, "Output"), CancellationToken.None);

        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNoOutput);
    }

    // ================= OUTPUT: phân bổ theo SL hoàn thiện, BreakEvenQty = NULL, IsAtRisk =================

    [Fact]
    public async Task CalculateAsync_RunAgainWithinTheSameSecond_UsesALaterCalculatedAt()
    {
        var period = Period();
        SetupReady(period);
        SetupProducts((1, "SP001", "Ao thun A"));
        _repo.GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<VwMonthlyRevenue> { Revenue(orderId: 10, productId: 1, qty: 100, revenue: 10_000_000m) });
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>())
            .Returns(new List<MonthlyCost> { new() { CostType = MonthlyCostType.Fixed, Amount = 1_000_000m } });
        // Lần tính trước đã ghi đúng giây "bây giờ": khóa UNIQUE (kỳ, CalculatedAt, mã hàng) sẽ báo trùng nếu dùng lại mốc này.
        _repo.GetResultsAsync(period.PeriodId, null, Arg.Any<CancellationToken>())
            .Returns(new List<BreakEvenResult> { new() { PeriodId = period.PeriodId, ProductId = 1, CalculatedAt = Now } });
        CaptureAddedResults(out var getAdded);

        var result = await _sut.CalculateAsync(new BreakEvenCalculateDto(Month, "Output"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        getAdded().Should().OnlyContain(r => r.CalculatedAt == Now.AddSeconds(1));
    }

    [Fact]
    public async Task CalculateAsync_OutputBasis_AllocatesFixedCostByFinishedQtyShare()
    {
        var period = Period();
        SetupReady(period);
        SetupProducts((1, "SP001", "Ao thun A"), (2, "SP002", "Ao thun B"));

        // Mã hàng 1: 100 sp, doanh thu 10,000,000 -> đơn giá 100,000. Biến phí 2,000,000 -> 20,000/sp.
        // Mã hàng 2: 50 sp, doanh thu 4,000,000 -> đơn giá 80,000. Biến phí 4,500,000 -> 90,000/sp > đơn giá
        // => không thể hòa vốn (BreakEvenQty NULL), thặng dư âm => IsAtRisk.
        _repo.GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>()).Returns(new List<VwMonthlyRevenue>
        {
            Revenue(orderId: 10, productId: 1, qty: 100, revenue: 10_000_000m),
            Revenue(orderId: 10, productId: 2, qty: 50, revenue: 4_000_000m),
        });
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(new List<MonthlyCost>
        {
            new() { CostType = MonthlyCostType.Fixed, Amount = 1_500_000m },
            new() { CostType = MonthlyCostType.Variable, ProductId = 1, Amount = 2_000_000m },
            new() { CostType = MonthlyCostType.Variable, ProductId = 2, Amount = 4_500_000m },
        });
        CaptureAddedResults(out var getAdded);

        var result = await _sut.CalculateAsync(new BreakEvenCalculateDto(Month, "Output"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);

        var p1 = result.Data.Items.Single(i => i.ProductId == 1);
        p1.AllocatedFixedCost.Should().Be(1_000_000m); // 1,500,000 * (100/150)
        p1.CmtUnitPrice.Should().Be(100_000m);
        p1.BreakEvenQty.Should().Be(12.5m);            // 1,000,000 / (100,000 - 20,000)
        p1.PercentReached.Should().Be(800m);           // 100 / 12.5 * 100
        p1.SurplusValue.Should().Be(7_000_000m);       // 10,000,000 - 2,000,000 - 1,000,000
        p1.IsAtRisk.Should().BeFalse();

        var p2 = result.Data.Items.Single(i => i.ProductId == 2);
        p2.AllocatedFixedCost.Should().Be(500_000m);   // 1,500,000 * (50/150)
        p2.CmtUnitPrice.Should().Be(80_000m);
        p2.BreakEvenQty.Should().BeNull();             // đơn giá <= biến phí/sp
        p2.PercentReached.Should().BeNull();
        p2.SurplusValue.Should().Be(-1_000_000m);      // 4,000,000 - 4,500,000 - 500,000
        p2.IsAtRisk.Should().BeTrue();
        p2.AtRiskWarning.Should().Contain("SP002").And.Contain("không thể đạt điểm hòa vốn");

        result.Data.AtRiskWarnings.Should().ContainSingle();

        var added = getAdded();
        added.Should().HaveCount(2);
        added.Should().OnlyContain(r => r.PeriodId == period.PeriodId && r.AllocationBasis == AllocationBasis.Output
                                         && r.CalculatedAt == Now);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        // Log cách tính + số mã hàng, KHÔNG chứa doanh thu / đơn giá CMT (dữ liệu mật, GBR-02/D26).
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(BreakEvenResult)
            && e.RecordId == period.PeriodId && e.Action == AuditActions.Insert
            && e.NewValues != null && !e.NewValues.ToString()!.Contains("Revenue")));
    }

    [Fact]
    public async Task CalculateAsync_NotReady_LogsNothing()
    {
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        await _sut.CalculateAsync(new BreakEvenCalculateDto(Month, "Output"), CancellationToken.None);

        _audit.DidNotReceiveWithAnyArgs().Add(default!);
    }

    // ================= LABOUR_HOURS: phân bổ theo giờ công quy đổi + fallback OUTPUT =================

    [Fact]
    public async Task CalculateAsync_LabourHoursBasis_AllocatesByAttributedHours_FallsBackToOutputWhenNoHours()
    {
        var period = Period();
        SetupReady(period);
        SetupProducts((1, "SP001", "Ao A"), (2, "SP002", "Ao B"), (3, "SP003", "Ao C"));

        // 3 mã hàng cùng hoàn thiện trong tháng; mã 3 không có OutputRecord nào trong dữ liệu giờ công
        // (ví dụ: output của nó không nằm trong khoảng ngày lấy mẫu) -> phải fallback sang tỉ lệ OUTPUT.
        _repo.GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>()).Returns(new List<VwMonthlyRevenue>
        {
            Revenue(orderId: 10, productId: 1, qty: 100, revenue: 10_000_000m),
            Revenue(orderId: 10, productId: 2, qty: 50, revenue: 5_000_000m),
            Revenue(orderId: 11, productId: 3, qty: 50, revenue: 5_000_000m),
        });
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(new List<MonthlyCost>
        {
            new() { CostType = MonthlyCostType.Fixed, Amount = 1_000_000m },
        });

        var workDate = new DateOnly(2026, 10, 5);
        _repo.GetOutputForLabourAsync(MonthStart, MonthEnd, Arg.Any<CancellationToken>())
            .Returns(new List<(int WorkerId, DateOnly WorkDate, int ProductId, int Quantity)>
            {
                (1, workDate, 1, 60),
                (1, workDate, 2, 40),
            });
        _repo.GetWorkedHoursAsync(Arg.Any<IReadOnlyCollection<int>>(), MonthStart, MonthEnd, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(int, DateOnly), decimal> { [(1, workDate)] = 8m });
        CaptureAddedResults(out var getAdded);

        var result = await _sut.CalculateAsync(
            new BreakEvenCalculateDto(Month, "LabourHours"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.AllocationBasis.Should().Be("LabourHours");

        // Giờ quy đổi: SP1 = 8 * 60/100 = 4.8h, SP2 = 8 * 40/100 = 3.2h, tổng trong phạm vi (SP1+SP2+SP3=0) = 8h.
        var p1 = result.Data.Items.Single(i => i.ProductId == 1);
        var p2 = result.Data.Items.Single(i => i.ProductId == 2);
        var p3 = result.Data.Items.Single(i => i.ProductId == 3);

        p1.AllocatedFixedCost.Should().Be(600_000m); // 1,000,000 * (4.8/8)
        p2.AllocatedFixedCost.Should().Be(400_000m); // 1,000,000 * (3.2/8)
        // SP3 không có giờ quy đổi -> fallback OUTPUT: share = 50/(100+50+50) = 0.25 -> 250,000
        p3.AllocatedFixedCost.Should().Be(250_000m);

        var added = getAdded();
        added.Should().OnlyContain(r => r.AllocationBasis == AllocationBasis.LabourHours);
    }

    // ================= GetBreakEvenAsync: lịch sử vs. lần tính gần nhất =================

    [Fact]
    public async Task GetBreakEvenAsync_HistoryFalse_ReturnsOnlyLatestRun()
    {
        var period = Period();
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(period);

        var product = new Product { ProductId = 1, ProductCode = "SP001", ProductName = "Ao A" };
        var older = new BreakEvenResult
        {
            BreakEvenResultId = 1, PeriodId = period.PeriodId, ProductId = 1, Product = product,
            AllocationBasis = AllocationBasis.Output, CalculatedAt = Now.AddDays(-1), FinishedQty = 100,
            CmtUnitPrice = 100_000m, Revenue = 10_000_000m, VariableCost = 2_000_000m, AllocatedFixedCost = 1_000_000m,
            SurplusValue = 7_000_000m, IsAtRisk = false,
        };
        var latest = new BreakEvenResult
        {
            BreakEvenResultId = 2, PeriodId = period.PeriodId, ProductId = 1, Product = product,
            AllocationBasis = AllocationBasis.LabourHours, CalculatedAt = Now, FinishedQty = 100,
            CmtUnitPrice = 100_000m, Revenue = 10_000_000m, VariableCost = 2_000_000m, AllocatedFixedCost = 900_000m,
            SurplusValue = 7_100_000m, IsAtRisk = false,
        };
        _repo.GetResultsAsync(period.PeriodId, null, Arg.Any<CancellationToken>())
            .Returns(new List<BreakEvenResult> { latest, older });

        var result = await _sut.GetBreakEvenAsync(Month, null, history: false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().ContainSingle(i => i.BreakEvenResultId == 2);
    }

    [Fact]
    public async Task GetBreakEvenAsync_HistoryTrue_ReturnsAllRuns()
    {
        var period = Period();
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        var product = new Product { ProductId = 1, ProductCode = "SP001", ProductName = "Ao A" };
        var rows = new List<BreakEvenResult>
        {
            new() { BreakEvenResultId = 1, PeriodId = period.PeriodId, ProductId = 1, Product = product,
                AllocationBasis = AllocationBasis.Output, CalculatedAt = Now.AddDays(-1), IsAtRisk = false },
            new() { BreakEvenResultId = 2, PeriodId = period.PeriodId, ProductId = 1, Product = product,
                AllocationBasis = AllocationBasis.LabourHours, CalculatedAt = Now, IsAtRisk = false },
        };
        _repo.GetResultsAsync(period.PeriodId, null, Arg.Any<CancellationToken>()).Returns(rows);

        var result = await _sut.GetBreakEvenAsync(Month, null, history: true, CancellationToken.None);

        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBreakEvenAsync_NoPeriod_ReturnsNotFound()
    {
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        var result = await _sut.GetBreakEvenAsync(Month, null, history: false, CancellationToken.None);

        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotFound);
    }

    // ================= Doanh thu (UC27) =================

    [Fact]
    public async Task GetRevenueAsync_ReturnsDtosWithOrderAndProductCodes_NoCmtUnitPrice()
    {
        _repo.GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>()).Returns(new List<VwMonthlyRevenue>
        {
            Revenue(orderId: 10, productId: 1, qty: 100, revenue: 10_000_000m),
        });
        _repo.GetOrderCodesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, string> { [10] = "DH001" });
        SetupProducts((1, "SP001", "Ao thun A"));

        var result = await _sut.GetRevenueAsync(new RevenueQuery { PeriodMonth = Month }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Data!.Items.Single();
        dto.OrderCode.Should().Be("DH001");
        dto.ProductCode.Should().Be("SP001");
        dto.FinishedQty.Should().Be(100);
        dto.Revenue.Should().Be(10_000_000m);

        // GBR-02: RevenueSummaryDto không bao giờ có trường đơn giá CMT (kiểm tra bằng reflection để chống
        // ai đó vô tình thêm lại CmtUnitPrice vào DTO doanh thu sau này).
        typeof(RevenueSummaryDto).GetProperties().Should().NotContain(p => p.Name == "CmtUnitPrice");
    }

    [Fact]
    public async Task GetRevenueAsync_InvalidMonth_ReturnsValidationFailure()
    {
        var result = await _sut.GetRevenueAsync(new RevenueQuery { PeriodMonth = "2026-13" }, CancellationToken.None);

        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.RevenuePeriodMonthInvalid);
    }
}
