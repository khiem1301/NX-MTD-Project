using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>UTCID588–UTCID659: FinanceService per UNIT_TEST_CASES_REBASELINE_v3.1 sheet FinanceService (P0-1).</summary>
public class FinanceServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 24, 10, 0, 0);
    private static readonly DateOnly Today = new(2026, 11, 24);
    private static readonly DateTime T1 = new(2026, 11, 1, 9, 0, 0);
    private static readonly DateTime T2 = new(2026, 11, 20, 15, 0, 0);
    private const string Month = "2026-10";
    private const int O1 = 1, O2 = 2, O9 = 9;
    private const int P1 = 1, P2 = 2, P3 = 3, P4 = 4;

    private readonly IFinanceRepository _repo = Substitute.For<IFinanceRepository>();
    private readonly IFinanceExcelWriter _writer = Substitute.For<IFinanceExcelWriter>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private List<VwMonthlyRevenue> _revenue = new();

    public FinanceServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _repo.GetRevenueRowsAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ci => FilterRevenue(ci.ArgAt<string?>(0), ci.ArgAt<int?>(1), ci.ArgAt<int?>(2)));
        _repo.GetOrderCodesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, string>());
        _repo.GetProductsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, string>());
        _repo.GetMonthlyCostsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<MonthlyCost>());
        _repo.GetResultsAsync(Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<BreakEvenResult>());
        _repo.GetOutputForLabourAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int WorkerId, DateOnly WorkDate, int ProductId, int Quantity)>());
        _repo.GetWorkedHoursAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(int, DateOnly), decimal>());
        _writer.WriteRevenueSheet(Arg.Any<IReadOnlyList<RevenueSummaryDto>>()).Returns(new byte[] { 1, 2, 3 });
        _writer.WriteBreakEvenSheet(Arg.Any<IReadOnlyList<BreakEvenResultDto>>()).Returns(new byte[] { 4, 5, 6 });
    }

    private FinanceService Sut() =>
        new(_repo, _writer, _audit, _clock, NullLogger<FinanceService>.Instance);

    private static VwMonthlyRevenue Rev(string month, int orderId, int productId, int? qty = 1, decimal? revenue = 0m) =>
        new()
        {
            PeriodMonth = month,
            OrderId = orderId,
            ProductId = productId,
            FinishedQty = qty,
            Revenue = revenue,
        };

    private static AccountingPeriod Period(string status = RecordStatus.Open, string month = Month)
    {
        var year = int.Parse(month[..4]);
        var mon = int.Parse(month[5..]);
        var start = new DateOnly(year, mon, 1);
        return new AccountingPeriod
        {
            PeriodId = 10,
            PeriodMonth = month,
            StartDate = start,
            EndDate = start.AddMonths(1).AddDays(-1),
            Status = status,
        };
    }

    private static MonthlyCost Fixed(decimal amount) =>
        new() { PeriodId = 10, CostType = MonthlyCostType.Fixed, Amount = amount };

    private static MonthlyCost Var(decimal amount, int? productId = null) =>
        new() { PeriodId = 10, CostType = MonthlyCostType.Variable, Amount = amount, ProductId = productId };

    private void SeedLookups(
        IEnumerable<(int Id, string Code)>? orders = null,
        IEnumerable<(int Id, string Code)>? products = null)
    {
        var orderMap = (orders ?? Array.Empty<(int, string)>())
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Code);
        var productMap = (products ?? Array.Empty<(int, string)>())
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Code);
        _repo.GetOrderCodesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns(orderMap);
        _repo.GetProductsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns(productMap);
    }

    /// <summary>One worker, one day: quantity proportional to hours so attribution is exact (P1=600, P2=400 → hours 1000).</summary>
    private void SeedLabour(IReadOnlyDictionary<int, decimal> hoursByProduct)
    {
        var day = new DateOnly(2026, 10, 15);
        const int workerId = 7;
        var rows = hoursByProduct
            .Where(kv => kv.Value > 0)
            .Select(kv => (WorkerId: workerId, WorkDate: day, ProductId: kv.Key, Quantity: (int)kv.Value))
            .ToList();
        var hours = hoursByProduct.Values.Where(h => h > 0).Sum();
        _repo.GetOutputForLabourAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(rows);
        _repo.GetWorkedHoursAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(int, DateOnly), decimal> { [(workerId, day)] = hours });
    }

    private List<BreakEvenResult> CaptureAdds()
    {
        var captured = new List<BreakEvenResult>();
        _repo.When(r => r.AddBreakEvenResults(Arg.Any<IEnumerable<BreakEvenResult>>()))
            .Do(c => captured.AddRange(c.Arg<IEnumerable<BreakEvenResult>>()));
        return captured;
    }

    private static RevenueQuery Q(string? periodMonth, int? orderId = null, int? productId = null, int page = 1, int pageSize = 10) =>
        new()
        {
            PeriodMonth = periodMonth,
            OrderId = orderId,
            ProductId = productId,
            Page = page,
            PageSize = pageSize,
        };

    private void SetupReady(AccountingPeriod period, bool payroll = true, bool hasCost = true)
    {
        _repo.FindPeriodByMonthAsync(period.PeriodMonth, Arg.Any<CancellationToken>()).Returns(period);
        _repo.IsPayrollApprovedAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(payroll);
        _repo.HasMonthlyCostAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(hasCost);
        _repo.GetResultsAsync(period.PeriodId, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<BreakEvenResult>());
        _repo.GetOutputForLabourAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int WorkerId, DateOnly WorkDate, int ProductId, int Quantity)>());
        _repo.GetWorkedHoursAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<(int, DateOnly), decimal>());
    }

    private AccountingPeriod SeedCalcBase(string status = RecordStatus.Open) =>
        ReadyWith(
            new[]
            {
                Rev(Month, O1, P1, 1000, 20_000_000m),
                Rev(Month, O1, P2, 500, 15_000_000m),
            },
            new[] { Fixed(9_000_000m), Var(8_000_000m, P1), Var(6_000_000m, P2) },
            new Dictionary<int, decimal> { [P1] = 600m, [P2] = 400m },
            status);

    private AccountingPeriod ReadyWith(
        IReadOnlyList<VwMonthlyRevenue> rows,
        IReadOnlyList<MonthlyCost> costs,
        IReadOnlyDictionary<int, decimal>? labour = null,
        string status = RecordStatus.Open,
        string month = Month)
    {
        var period = Period(status, month);
        SetupReady(period);
        _revenue = rows.ToList();
        SeedLookups(
            rows.Select(r => (Id: r.OrderId, Code: "O" + r.OrderId)),
            rows.Select(r => (Id: r.ProductId, Code: "P" + r.ProductId)));
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(costs.ToList());
        if (labour is not null)
            SeedLabour(labour);
        return period;
    }

    private void SeedOctoberView()
    {
        _revenue = new List<VwMonthlyRevenue>
        {
            Rev(Month, O1, P1, 100, 1_000_000m),
            Rev(Month, O1, P2, 50, 500_000m),
            Rev(Month, O2, P3, 20, 200_000m),
        };
        SeedLookups(
            new[] { (Id: O1, Code: "O1"), (Id: O2, Code: "O2") },
            new[] { (Id: P1, Code: "P1"), (Id: P2, Code: "P2"), (Id: P3, Code: "P3") });
    }

    private void SeedFiveMonths()
    {
        _revenue = new List<VwMonthlyRevenue>
        {
            Rev("2026-09", O1, P1, 10, 100m),
            Rev("2026-09", O2, P2, 10, 100m),
            Rev(Month, O1, P1, 10, 100m),
            Rev(Month, O1, P2, 10, 100m),
            Rev(Month, O2, P3, 10, 100m),
        };
        SeedLookups(
            new[] { (Id: O1, Code: "O1"), (Id: O2, Code: "O2") },
            new[] { (Id: P1, Code: "P1"), (Id: P2, Code: "P2"), (Id: P3, Code: "P3") });
    }

    private IReadOnlyList<VwMonthlyRevenue> FilterRevenue(string? month, int? orderId, int? productId)
    {
        IEnumerable<VwMonthlyRevenue> query = _revenue;
        if (!string.IsNullOrWhiteSpace(month))
            query = query.Where(r => r.PeriodMonth == month);
        if (orderId is int oid)
            query = query.Where(r => r.OrderId == oid);
        if (productId is int pid)
            query = query.Where(r => r.ProductId == pid);
        return query.ToList();
    }

    private static BreakEvenResult Row(int id, int productId, string code, string basis, DateTime at, decimal allocated) =>
        new()
        {
            BreakEvenResultId = id,
            PeriodId = 10,
            ProductId = productId,
            Product = new Product { ProductId = productId, ProductCode = code, Status = RecordStatus.Active },
            AllocationBasis = basis,
            CalculatedAt = at,
            AllocatedFixedCost = allocated,
            FinishedQty = 100,
            CmtUnitPrice = 10_000m,
            Revenue = 1_000_000m,
            VariableCost = 100_000m,
            SurplusValue = 1m,
            IsAtRisk = false,
        };

    private List<BreakEvenResult> TwoRuns() =>
    [
        Row(1, P1, "P1", AllocationBasis.Output, T1, 6_000_000m),
        Row(2, P1, "P1", AllocationBasis.LabourHours, T1, 7_000_000m),
        Row(3, P2, "P2", AllocationBasis.Output, T1, 4_000_000m),
        Row(4, P2, "P2", AllocationBasis.LabourHours, T1, 3_000_000m),
        Row(5, P1, "P1", AllocationBasis.Output, T2, 6_000_000m),
        Row(6, P1, "P1", AllocationBasis.LabourHours, T2, 7_000_000m),
        Row(7, P2, "P2", AllocationBasis.Output, T2, 4_000_000m),
        Row(8, P2, "P2", AllocationBasis.LabourHours, T2, 3_000_000m),
    ];

    private void SeedResults(IReadOnlyList<BreakEvenResult> rows, string month = Month)
    {
        var period = Period(month: month);
        _repo.FindPeriodByMonthAsync(month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.GetResultsAsync(period.PeriodId, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var productId = ci.ArgAt<int?>(1);
                IEnumerable<BreakEvenResult> query = rows;
                if (productId is int id)
                    query = query.Where(r => r.ProductId == id);
                return query.ToList();
            });
    }

    // ================= GetRevenueAsync UTCID588–603 =================

    [Fact]
    public async Task UTCID588_GetRevenueAsync_OctoberPage_ReturnsThreeCodedRows()
    {
        // Arrange
        SeedOctoberView();

        // Act
        var result = await Sut().GetRevenueAsync(Q(Month, page: 1, pageSize: 10), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalItems.Should().Be(3);
        result.Data.Items.Select(i => $"{i.OrderCode}/{i.ProductCode}").Should().Equal("O1/P1", "O1/P2", "O2/P3");
        await _repo.Received(1).GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>());
        await _repo.Received(1).GetOrderCodesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).GetProductsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID589_GetRevenueAsync_FilterOrder_ReturnsTwoRows()
    {
        // Arrange
        SeedOctoberView();

        // Act
        var result = await Sut().GetRevenueAsync(Q(Month, orderId: O1), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalItems.Should().Be(2);
        result.Data.Items.Should().OnlyContain(i => i.OrderId == O1 && i.OrderCode == "O1");
        await _repo.Received(1).GetRevenueRowsAsync(Month, O1, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID590_GetRevenueAsync_FilterProduct_ReturnsOneRow()
    {
        // Arrange
        SeedOctoberView();

        // Act
        var result = await Sut().GetRevenueAsync(Q(Month, productId: P2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalItems.Should().Be(1);
        result.Data.Items.Should().ContainSingle(i => i.ProductId == P2 && i.ProductCode == "P2");
        await _repo.Received(1).GetRevenueRowsAsync(Month, null, P2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID591_GetRevenueAsync_NullMonth_ReturnsAllFiveRows()
    {
        // Arrange
        SeedFiveMonths();

        // Act
        var result = await Sut().GetRevenueAsync(Q(null), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.TotalItems.Should().Be(5);
        result.Data.Items.Should().HaveCount(5);
        await _repo.Received(1).GetRevenueRowsAsync(null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID592_GetRevenueAsync_EmptyMonth_TreatedAsUnfiltered()
    {
        // Arrange
        SeedFiveMonths();

        // Act
        var result = await Sut().GetRevenueAsync(Q(""), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.Data!.TotalItems.Should().Be(5);
        await _repo.Received(1).GetRevenueRowsAsync("", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID593_GetRevenueAsync_January_IsValidMonth()
    {
        // Arrange
        _revenue.Clear();

        // Act
        var result = await Sut().GetRevenueAsync(Q("2026-01"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        await _repo.Received(1).GetRevenueRowsAsync("2026-01", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID594_GetRevenueAsync_December_IsValidMonth()
    {
        // Arrange
        _revenue.Clear();

        // Act
        var result = await Sut().GetRevenueAsync(Q("2026-12"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        await _repo.Received(1).GetRevenueRowsAsync("2026-12", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID595_GetRevenueAsync_EmptyMonth_ReturnsNoData()
    {
        // Arrange
        _revenue.Clear();

        // Act
        var result = await Sut().GetRevenueAsync(Q("2026-11"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
        result.Data.TotalItems.Should().Be(0);
        result.Message.Should().Be(Messages.NoData);
        await _repo.Received(1).GetRevenueRowsAsync("2026-11", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID596_GetRevenueAsync_SecondPage_ReturnsRemainder()
    {
        // Arrange
        SeedOctoberView();

        // Act
        var result = await Sut().GetRevenueAsync(Q(Month, page: 2, pageSize: 2), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        result.Data.TotalItems.Should().Be(3);
        result.Data.Page.Should().Be(2);
        result.Data.PageSize.Should().Be(2);
        await _repo.Received(1).GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID597_GetRevenueAsync_ZeroPage_NormalizesToDefaults()
    {
        // Arrange
        SeedOctoberView();

        // Act
        var result = await Sut().GetRevenueAsync(Q(Month, page: 0, pageSize: 0), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Page.Should().Be(1);
        result.Data.PageSize.Should().Be(20);
        result.Data.TotalItems.Should().Be(3);
        result.Data.Items.Should().HaveCount(3);
        await _repo.Received(1).GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID598_GetRevenueAsync_HugePageSize_CappedAt100()
    {
        // Arrange
        SeedOctoberView();

        // Act
        var result = await Sut().GetRevenueAsync(Q(Month, pageSize: 10000), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.PageSize.Should().Be(100);
        result.Data.TotalItems.Should().Be(3);
        await _repo.Received(1).GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID599_GetRevenueAsync_NullQtyAndRevenue_BecomeZero()
    {
        // Arrange
        _revenue = new List<VwMonthlyRevenue> { Rev(Month, O1, P1, null, null) };
        SeedLookups(new[] { (Id: O1, Code: "O1") }, new[] { (Id: P1, Code: "P1") });

        // Act
        var result = await Sut().GetRevenueAsync(Q(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var item = result.Data!.Items.Should().ContainSingle().Subject;
        item.FinishedQty.Should().Be(0);
        item.Revenue.Should().Be(0m);
        await _repo.Received(1).GetProductsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID600_GetRevenueAsync_Month13_ValidationAndRepoNotCalled()
    {
        // Act
        var result = await Sut().GetRevenueAsync(Q("2026-13"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.RevenuePeriodMonthInvalid);
        await _repo.DidNotReceiveWithAnyArgs().GetRevenueRowsAsync(default!, default, default, default);
    }

    [Fact]
    public async Task UTCID601_GetRevenueAsync_SlashMonth_Validation()
    {
        // Act
        var result = await Sut().GetRevenueAsync(Q("2026/10"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.RevenuePeriodMonthInvalid);
        await _repo.DidNotReceiveWithAnyArgs().GetRevenueRowsAsync(default!, default, default, default);
    }

    [Fact]
    public async Task UTCID602_GetRevenueAsync_UnknownOrder_EmptyCodeNoException()
    {
        // Arrange
        _revenue = new List<VwMonthlyRevenue> { Rev(Month, O9, P1, 10, 100m) };
        SeedLookups(Array.Empty<(int Id, string Code)>(), new[] { (Id: P1, Code: "P1") });

        // Act
        var result = await Sut().GetRevenueAsync(Q(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.Data!.Items.Should().ContainSingle().Which.OrderCode.Should().Be("");
        await _repo.Received(1).GetOrderCodesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID603_GetRevenueAsync_NullQuery_ArgumentNullException()
    {
        // Act
        var act = async () => await Sut().GetRevenueAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
        await _repo.DidNotReceiveWithAnyArgs().GetRevenueRowsAsync(default!, default, default, default);
    }

    // ================= GetRevenueFilterOptionsAsync UTCID604–609 =================

    [Fact]
    public async Task UTCID604_GetRevenueFilterOptionsAsync_SortsOrdersAndProductsByCode()
    {
        // Arrange
        _revenue = new List<VwMonthlyRevenue>
        {
            Rev(Month, O2, P3, 1, 1m),
            Rev(Month, O1, P2, 1, 1m),
            Rev(Month, O1, P1, 1, 1m),
        };
        SeedLookups(
            new[] { (Id: O2, Code: "O2"), (Id: O1, Code: "O1") },
            new[] { (Id: P3, Code: "P3"), (Id: P1, Code: "P1"), (Id: P2, Code: "P2") });

        // Act
        var result = await Sut().GetRevenueFilterOptionsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Orders.Select(o => o.OrderCode).Should().Equal("O1", "O2");
        result.Data.Products.Select(p => p.ProductCode).Should().Equal("P1", "P2", "P3");
        await _repo.Received(1).GetRevenueRowsAsync(null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID605_GetRevenueFilterOptionsAsync_EmptyView_EmptyOptions()
    {
        // Arrange
        _revenue.Clear();

        // Act
        var result = await Sut().GetRevenueFilterOptionsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Orders.Should().BeEmpty();
        result.Data.Products.Should().BeEmpty();
        await _repo.Received(1).GetRevenueRowsAsync(null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID606_GetRevenueFilterOptionsAsync_SameOrderThreeMonths_GroupedOnce()
    {
        // Arrange
        _revenue = new List<VwMonthlyRevenue>
        {
            Rev("2026-08", O1, P1, 1, 1m),
            Rev("2026-09", O1, P1, 1, 1m),
            Rev(Month, O1, P1, 1, 1m),
        };
        SeedLookups(new[] { (Id: O1, Code: "O1") }, new[] { (Id: P1, Code: "P1") });

        // Act
        var result = await Sut().GetRevenueFilterOptionsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Orders.Should().ContainSingle(o => o.OrderId == O1 && o.OrderCode == "O1");
        await _repo.Received(1).GetRevenueRowsAsync(null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID607_GetRevenueFilterOptionsAsync_ConflictingCodes_TakesFirstNoException()
    {
        // Arrange
        _revenue = new List<VwMonthlyRevenue>
        {
            Rev(Month, O1, P1, 1, 1m),
            Rev("2026-09", O1, P2, 1, 1m),
        };
        var firstWins = new Dictionary<int, string>();
        firstWins.TryAdd(O1, "CODE-FIRST");
        firstWins.TryAdd(O1, "CODE-SECOND");
        _repo.GetOrderCodesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns(firstWins);
        SeedLookups(products: new[] { (Id: P1, Code: "P1"), (Id: P2, Code: "P2") });
        _repo.GetOrderCodesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns(firstWins);

        // Act
        var result = await Sut().GetRevenueFilterOptionsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Orders.Should().ContainSingle().Which.OrderCode.Should().Be("CODE-FIRST");
        await _repo.Received(1).GetOrderCodesAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID608_GetRevenueFilterOptionsAsync_SoftDeletedProduct_StillListed()
    {
        // Arrange
        _revenue = new List<VwMonthlyRevenue> { Rev(Month, O1, P3, 5, 50m) };
        SeedLookups(new[] { (Id: O1, Code: "O1") }, new[] { (Id: P3, Code: "P3") });

        // Act
        var result = await Sut().GetRevenueFilterOptionsAsync(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Products.Should().ContainSingle(p => p.ProductId == P3 && p.ProductCode == "P3");
        await _repo.Received(1).GetProductsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID609_GetRevenueFilterOptionsAsync_RepoThrows_Propagates()
    {
        // Arrange
        _repo.GetRevenueRowsAsync(Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("repo"));

        // Act
        var act = async () => await Sut().GetRevenueFilterOptionsAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ================= ExportRevenueAsync UTCID610–614 =================

    [Fact]
    public async Task UTCID610_ExportRevenueAsync_ThreeRows_WritesDoanhThu()
    {
        // Arrange
        SeedOctoberView();

        // Act
        var result = await Sut().ExportRevenueAsync(Q(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(Messages.ExcelExported);
        result.Data!.FileName.Should().Be("DoanhThu.xlsx");
        result.Data.Content.Should().NotBeEmpty();
        _writer.Received(1).WriteRevenueSheet(Arg.Is<IReadOnlyList<RevenueSummaryDto>>(rows => rows.Count == 3));
        await _repo.Received(1).GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID611_ExportRevenueAsync_NoRows_WriterGetsEmptyList()
    {
        // Arrange
        _revenue.Clear();

        // Act
        var result = await Sut().ExportRevenueAsync(Q("2026-11"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _writer.Received(1).WriteRevenueSheet(Arg.Is<IReadOnlyList<RevenueSummaryDto>>(rows => rows.Count == 0));
    }

    [Fact]
    public async Task UTCID612_ExportRevenueAsync_NullMonth_ExportsAllFiveUnpaged()
    {
        // Arrange
        SeedFiveMonths();

        // Act
        var result = await Sut().ExportRevenueAsync(Q(null, page: 1, pageSize: 1), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.FileName.Should().Be("DoanhThu.xlsx");
        _writer.Received(1).WriteRevenueSheet(Arg.Is<IReadOnlyList<RevenueSummaryDto>>(rows => rows.Count == 5));
    }

    [Fact]
    public async Task UTCID613_ExportRevenueAsync_Month00_WriterNotCalled()
    {
        // Act
        var result = await Sut().ExportRevenueAsync(Q("2026-00"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.RevenuePeriodMonthInvalid);
        result.Kind.Should().Be(ResultKind.Validation);
        _writer.DidNotReceiveWithAnyArgs().WriteRevenueSheet(default!);
        await _repo.DidNotReceiveWithAnyArgs().GetRevenueRowsAsync(default!, default, default, default);
    }

    [Fact]
    public async Task UTCID614_ExportRevenueAsync_WriterThrows_Propagates()
    {
        // Arrange
        SeedOctoberView();
        _writer.WriteRevenueSheet(Arg.Any<IReadOnlyList<RevenueSummaryDto>>())
            .Throws(new InvalidOperationException("writer"));

        // Act
        var act = async () => await Sut().ExportRevenueAsync(Q(Month), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ================= CalculateAsync UTCID615–634 =================

    [Fact]
    public async Task UTCID615_CalculateAsync_TwoProductsBothBases_AllocatesAndPersists()
    {
        // Arrange
        var period = SeedCalcBase();
        var added = CaptureAdds();

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(4);
        var p1Out = result.Data.Items.Single(i => i.ProductId == P1 && i.AllocationBasis == "Output");
        p1Out.AllocatedFixedCost.Should().Be(6_000_000m);
        p1Out.CmtUnitPrice.Should().Be(20_000m);
        (p1Out.VariableCost / p1Out.FinishedQty).Should().Be(8_000m);
        p1Out.BreakEvenQty.Should().Be(500m);
        p1Out.PercentReached.Should().Be(200m);
        p1Out.SurplusValue.Should().Be(6_000_000m);
        p1Out.IsAtRisk.Should().BeFalse();
        var p1Labour = result.Data.Items.Single(i => i.ProductId == P1 && i.AllocationBasis == "LabourHours");
        p1Labour.AllocatedFixedCost.Should().Be(5_400_000m);
        added.Should().HaveCount(4);
        added.Select(e => e.AllocationBasis).Distinct().Should().BeEquivalentTo(AllocationBasis.Output, AllocationBasis.LabourHours);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e =>
            e.TableName == nameof(BreakEvenResult) && e.Action == AuditActions.Insert && e.RecordId == period.PeriodId));
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID616_CalculateAsync_ThirdProductAtRisk_WarningDistinctOnce()
    {
        // Arrange — 3 mã qty bằng nhau (500) → OUTPUT Allocated = 9M/3 = 3M (khớp expected bảng)
        var period = SeedCalcBase();
        _revenue =
        [
            Rev(Month, O1, P1, 500, 20_000_000m),
            Rev(Month, O1, P2, 500, 15_000_000m),
            Rev(Month, O2, P3, 500, 5_000_000m),
        ];
        SeedLookups(
            new[] { (Id: O1, Code: "O1"), (Id: O2, Code: "O2") },
            new[] { (Id: P1, Code: "P1"), (Id: P2, Code: "P2"), (Id: P3, Code: "P3") });
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(new List<MonthlyCost>
        {
            Fixed(9_000_000m), Var(8_000_000m, P1), Var(6_000_000m, P2), Var(4_000_000m, P3),
        });
        SeedLabour(new Dictionary<int, decimal> { [P1] = 600m, [P2] = 400m, [P3] = 0m });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var p3 = result.Data!.Items.Where(i => i.ProductId == P3).ToList();
        p3.Should().HaveCount(2);
        p3.Should().OnlyContain(i => i.IsAtRisk);
        p3.Single(i => i.AllocationBasis == "Output").AllocatedFixedCost.Should().Be(3_000_000m);
        p3.Single(i => i.AllocationBasis == "Output").SurplusValue.Should().Be(-2_000_000m);
        // Expected: P3 đúng 1 lần trong AtRiskWarnings. ⚠ Code Distinct cả câu MSG (khác % theo basis) → còn 2.
        result.Data.AtRiskWarnings.Should().ContainSingle(w => w.Contains("P3"));
        _audit.Received(1).Add(Arg.Any<AuditEntry>());
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID617_CalculateAsync_CmtEqualsVariablePerUnit_BreakEvenNull()
    {
        // Arrange
        ReadyWith(
            new[] { Rev(Month, O1, P1, 1000, 10_000_000m) },
            new[] { Fixed(1_000_000m), Var(10_000_000m, P1) });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().OnlyContain(i => i.BreakEvenQty == null && i.PercentReached == null);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID618_CalculateAsync_CmtBelowVariablePerUnit_AtRiskAndNullBreakEven()
    {
        // Arrange
        ReadyWith(
            new[] { Rev(Month, O1, P1, 1000, 10_000_000m) },
            new[] { Fixed(1_000_000m), Var(12_000_000m, P1) });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().OnlyContain(i => i.BreakEvenQty == null && i.PercentReached == null && i.IsAtRisk);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID619_CalculateAsync_ZeroFixed_BreakEvenZeroPercent100()
    {
        // Arrange
        ReadyWith(
            new[] { Rev(Month, O1, P1, 1000, 10_000_000m) },
            new[] { Fixed(0m) });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().OnlyContain(i =>
            i.AllocatedFixedCost == 0m && i.BreakEvenQty == 0m && i.PercentReached == 100m);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID620_CalculateAsync_ZeroFinishedQty_SurplusUsesRevenueMinusVarMinusAllocated()
    {
        // Arrange
        ReadyWith(
            new[] { Rev(Month, O1, P4, 0, 2_000_000m) },
            new[] { Fixed(1_000_000m), Var(500_000m, P4) });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var p4 = result.Data!.Items.Where(i => i.ProductId == P4).ToList();
        p4.Should().OnlyContain(i => i.CmtUnitPrice == 0m && i.FinishedQty == 0 && i.BreakEvenQty == null);
        p4.Should().OnlyContain(i => i.SurplusValue == i.Revenue - i.VariableCost - i.AllocatedFixedCost);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID621_CalculateAsync_AllQtyZero_OutputAllocatedZeroNoDivideByZero()
    {
        // Arrange
        ReadyWith(
            new[]
            {
                Rev(Month, O1, P1, 0, 1_000_000m),
                Rev(Month, O1, P2, 0, 1_000_000m),
            },
            new[] { Fixed(8_000_000m) });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Where(i => i.AllocationBasis == "Output")
            .Should().OnlyContain(i => i.AllocatedFixedCost == 0m);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID622_CalculateAsync_MissingLabourHours_FallbackThenNormalizeTo100()
    {
        // Arrange
        ReadyWith(
            new[]
            {
                Rev(Month, O1, P1, 1000, 10_000_000m),
                Rev(Month, O1, P2, 500, 5_000_000m),
            },
            new[] { Fixed(9_000_000m) },
            new Dictionary<int, decimal> { [P1] = 600m });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var labour = result.Data!.Items.Where(i => i.AllocationBasis == "LabourHours").ToList();
        labour.Sum(i => i.AllocatedFixedCost).Should().Be(9_000_000m);
        labour.Sum(i => i.SharePct).Should().Be(100m);
        labour.Single(i => i.ProductId == P2).AllocatedFixedCost.Should().BeGreaterThan(0);
        labour.Single(i => i.ProductId == P1).SharePct.Should().BeLessThan(100m);
        await _repo.Received(1).GetOutputForLabourAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID623_CalculateAsync_ThreeEqualShares_RoundingLeavesOneCent()
    {
        // Arrange
        ReadyWith(
            new[]
            {
                Rev(Month, O1, P1, 100, 1_000_000m),
                Rev(Month, O1, P2, 100, 1_000_000m),
                Rev(Month, O2, P3, 100, 1_000_000m),
            },
            new[] { Fixed(10_000_000m) });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var output = result.Data!.Items.Where(i => i.AllocationBasis == "Output").ToList();
        output.Should().HaveCount(3);
        output.Should().OnlyContain(i => i.AllocatedFixedCost == 3_333_333.33m);
        output.Sum(i => i.AllocatedFixedCost).Should().Be(9_999_999.99m);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID624_CalculateAsync_SurplusExactlyZero_NotAtRisk()
    {
        // Arrange
        ReadyWith(
            new[] { Rev(Month, O1, P1, 1000, 10_000_000m) },
            new[] { Fixed(6_000_000m), Var(4_000_000m, P1) });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().OnlyContain(i => i.SurplusValue == 0m && i.IsAtRisk == false);
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID625_CalculateAsync_SameSecond_BumpsCalculatedAtByOneSecond()
    {
        // Arrange
        var period = SeedCalcBase();
        var added = CaptureAdds();
        _repo.GetResultsAsync(period.PeriodId, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<BreakEvenResult> { new() { PeriodId = period.PeriodId, ProductId = P1, CalculatedAt = Now } });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.CalculatedAt.Should().Be(Now.AddSeconds(1));
        added.Should().OnlyContain(e => e.CalculatedAt == Now.AddSeconds(1));
        await _repo.Received().GetResultsAsync(period.PeriodId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID626_CalculateAsync_UnassignedVariable_AllocatedOrWarned()
    {
        // Arrange
        ReadyWith(
            new[] { Rev(Month, O1, P1, 1000, 20_000_000m) },
            new[] { Fixed(1_000_000m), Var(2_000_000m, null) });

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var variableAllocated = result.Data!.Items.Where(i => i.AllocationBasis == "Output").Sum(i => i.VariableCost);
        var warned = result.Data.AtRiskWarnings.Count > 0 || result.Data.Items.Any(i => i.AtRiskWarning is not null);
        (variableAllocated == 2_000_000m || warned).Should().BeTrue();
        await _repo.Received(1).GetMonthlyCostsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID627_CalculateAsync_Month13_ValidationRepoNotCalled()
    {
        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto("2026-13"), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenPeriodMonthInvalid);
        await _repo.DidNotReceiveWithAnyArgs().FindPeriodByMonthAsync(default!, default);
        await _repo.DidNotReceiveWithAnyArgs().GetRevenueRowsAsync(default!, default, default, default);
        _audit.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task UTCID628_CalculateAsync_NullMonth_Validation()
    {
        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(null!), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenPeriodMonthInvalid);
        await _repo.DidNotReceiveWithAnyArgs().FindPeriodByMonthAsync(default!, default);
    }

    [Fact]
    public async Task UTCID629_CalculateAsync_NoPeriod_NotReady()
    {
        // Arrange
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotReady);
        await _repo.Received(1).FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>());
        await _repo.DidNotReceiveWithAnyArgs().GetRevenueRowsAsync(default!, default, default, default);
        _repo.DidNotReceiveWithAnyArgs().AddBreakEvenResults(default!);
        await _repo.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UTCID630_CalculateAsync_PayrollNotApproved_NotReady()
    {
        // Arrange
        var period = Period();
        SetupReady(period, payroll: false, hasCost: true);

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotReady);
        await _repo.Received(1).IsPayrollApprovedAsync(period.PeriodId, Arg.Any<CancellationToken>());
        _repo.DidNotReceiveWithAnyArgs().AddBreakEvenResults(default!);
        await _repo.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UTCID631_CalculateAsync_NoMonthlyCost_NotReady()
    {
        // Arrange
        var period = Period();
        SetupReady(period, payroll: true, hasCost: false);

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotReady);
        await _repo.Received(1).HasMonthlyCostAsync(period.PeriodId, Arg.Any<CancellationToken>());
        await _repo.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UTCID632_CalculateAsync_NoRevenue_NoOutputAndNoSave()
    {
        // Arrange
        var period = Period();
        SetupReady(period);
        _revenue.Clear();

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNoOutput);
        await _repo.Received(1).GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>());
        _repo.DidNotReceiveWithAnyArgs().AddBreakEvenResults(default!);
        await _repo.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task UTCID633_CalculateAsync_LockedPeriod_StillSucceeds()
    {
        // Arrange
        var period = SeedCalcBase(RecordStatus.Locked);
        period.Status.Should().Be(RecordStatus.Locked);

        // Act
        var result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ErrorCode.Should().BeNull();
        result.Data!.Items.Should().NotBeEmpty();
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID634_CalculateAsync_SaveThrowsDbUpdate_ConflictOrPropagates()
    {
        // Arrange
        SeedCalcBase();
        _repo.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DbUpdateException("UNIQUE (PeriodId, CalculatedAt, ProductId)"));

        // Act
        Exception? thrown = null;
        Result<BreakEvenCalculationResultDto>? result = null;
        try
        {
            result = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        // Assert
        if (thrown is not null)
            thrown.Should().BeOfType<DbUpdateException>();
        else
        {
            result!.IsSuccess.Should().BeFalse();
            result.Kind.Should().Be(ResultKind.Conflict);
        }
        await _repo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ================= GetBreakEvenInputsAsync UTCID635–644 =================

    [Fact]
    public async Task UTCID635_GetBreakEvenInputsAsync_ReadyOpenPeriod_CanCalculate()
    {
        // Arrange
        var period = Period();
        SetupReady(period, payroll: true, hasCost: true);
        _revenue = new List<VwMonthlyRevenue>
        {
            Rev(Month, O1, P1, 1000, 20_000_000m),
            Rev(Month, O1, P2, 500, 15_000_000m),
        };
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(new List<MonthlyCost>
        {
            Fixed(9_000_000m), Var(8_000_000m, P1), Var(6_000_000m, P2),
        });
        SeedLabour(new Dictionary<int, decimal> { [P1] = 600m, [P2] = 400m });

        // Act
        var result = await Sut().GetBreakEvenInputsAsync(Month, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(new BreakEvenInputsDto(
            true, false, true, true, 9_000_000m, 14_000_000m, 2, 2, 1500, 35_000_000m, 1000m, true));
        await _repo.Received(1).GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>());
        await _repo.Received(1).GetOutputForLabourAsync(period.StartDate, period.EndDate, Arg.Any<CancellationToken>());
        await _repo.Received(1).GetWorkedHoursAsync(
            Arg.Any<IReadOnlyCollection<int>>(), period.StartDate, period.EndDate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID636_GetBreakEvenInputsAsync_LockedPeriod_StillCanCalculate()
    {
        // Arrange
        var period = Period(RecordStatus.Locked);
        SetupReady(period, payroll: true, hasCost: true);
        _revenue = new List<VwMonthlyRevenue>
        {
            Rev(Month, O1, P1, 1000, 20_000_000m),
            Rev(Month, O1, P2, 500, 15_000_000m),
        };
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(new List<MonthlyCost>
        {
            Fixed(9_000_000m), Var(8_000_000m, P1), Var(6_000_000m, P2),
        });
        SeedLabour(new Dictionary<int, decimal> { [P1] = 600m, [P2] = 400m });

        // Act
        var result = await Sut().GetBreakEvenInputsAsync(Month, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.PeriodLocked.Should().BeTrue();
        result.Data.CanCalculate.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(new BreakEvenInputsDto(
            true, true, true, true, 9_000_000m, 14_000_000m, 2, 2, 1500, 35_000_000m, 1000m, true));
        await _repo.Received(1).FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID637_GetBreakEvenInputsAsync_MissingPeriod_SuccessWithViewTotals()
    {
        // Arrange
        _revenue = new List<VwMonthlyRevenue> { Rev("2026-09", O1, P1, 80, 4_000_000m) };
        _repo.FindPeriodByMonthAsync("2026-09", Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        // Act
        var result = await Sut().GetBreakEvenInputsAsync("2026-09", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Kind.Should().NotBe(ResultKind.NotFound);
        result.Data.Should().BeEquivalentTo(new BreakEvenInputsDto(
            false, false, false, false, 0m, 0m, 0, 1, 80, 4_000_000m, 0m, false));
        await _repo.DidNotReceiveWithAnyArgs().IsPayrollApprovedAsync(default, default);
        await _repo.DidNotReceiveWithAnyArgs().GetMonthlyCostsAsync(default, default);
    }

    [Fact]
    public async Task UTCID638_GetBreakEvenInputsAsync_NoCosts_CannotCalculate()
    {
        // Arrange
        var period = Period();
        SetupReady(period, payroll: true, hasCost: true);
        _revenue = new List<VwMonthlyRevenue> { Rev(Month, O1, P1, 10, 1_000m) };
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(new List<MonthlyCost>());

        // Act
        var result = await Sut().GetBreakEvenInputsAsync(Month, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.CostEntered.Should().BeFalse();
        result.Data.CanCalculate.Should().BeFalse();
        result.Data.PayrollApproved.Should().BeTrue();
        await _repo.Received(1).GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID639_GetBreakEvenInputsAsync_NoProducts_CannotCalculate()
    {
        // Arrange
        var period = Period();
        SetupReady(period, payroll: true, hasCost: true);
        _revenue.Clear();
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(
            new List<MonthlyCost> { Fixed(100m), Var(50m, P1) });

        // Act
        var result = await Sut().GetBreakEvenInputsAsync(Month, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.ProductCount.Should().Be(0);
        result.Data.CostEntered.Should().BeTrue();
        result.Data.CanCalculate.Should().BeFalse();
        await _repo.Received(1).GetRevenueRowsAsync(Month, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID640_GetBreakEvenInputsAsync_ZeroLabour_StillCanCalculate()
    {
        // Arrange
        var period = Period();
        SetupReady(period, payroll: true, hasCost: true);
        _revenue = new List<VwMonthlyRevenue> { Rev(Month, O1, P1, 10, 1_000m) };
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(
            new List<MonthlyCost> { Fixed(100m) });

        // Act
        var result = await Sut().GetBreakEvenInputsAsync(Month, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.LabourHours.Should().Be(0m);
        result.Data.CanCalculate.Should().BeTrue();
        await _repo.Received(1).GetOutputForLabourAsync(period.StartDate, period.EndDate, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID641_GetBreakEvenInputsAsync_PayrollDraft_CannotCalculate()
    {
        // Arrange
        var period = Period();
        SetupReady(period, payroll: false, hasCost: true);
        _revenue = new List<VwMonthlyRevenue> { Rev(Month, O1, P1, 10, 1_000m) };
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(
            new List<MonthlyCost> { Fixed(100m) });

        // Act
        var result = await Sut().GetBreakEvenInputsAsync(Month, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.PayrollApproved.Should().BeFalse();
        result.Data.CanCalculate.Should().BeFalse();
        await _repo.Received(1).IsPayrollApprovedAsync(period.PeriodId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID642_GetBreakEvenInputsAsync_Month13_Validation()
    {
        // Act
        var result = await Sut().GetBreakEvenInputsAsync("2026-13", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenPeriodMonthInvalid);
        await _repo.DidNotReceiveWithAnyArgs().FindPeriodByMonthAsync(default!, default);
    }

    [Fact]
    public async Task UTCID643_GetBreakEvenInputsAsync_NullMonth_Validation()
    {
        // Act
        var result = await Sut().GetBreakEvenInputsAsync(null!, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenPeriodMonthInvalid);
        await _repo.DidNotReceiveWithAnyArgs().FindPeriodByMonthAsync(default!, default);
    }

    [Fact]
    public async Task UTCID644_GetBreakEvenInputsAsync_CanCalculateAgreesWithCalculate()
    {
        // Arrange
        var period = Period();
        SetupReady(period, payroll: true, hasCost: false);
        _revenue = new List<VwMonthlyRevenue> { Rev(Month, O1, P1, 100, 1_000_000m) };
        _repo.GetMonthlyCostsAsync(period.PeriodId, Arg.Any<CancellationToken>()).Returns(
            new List<MonthlyCost> { Fixed(1_000m), Var(2_000m, P1) });

        // Act
        var inputs = await Sut().GetBreakEvenInputsAsync(Month, CancellationToken.None);
        var calc = await Sut().CalculateAsync(new BreakEvenCalculateDto(Month), CancellationToken.None);

        // Assert
        inputs.IsSuccess.Should().BeTrue();
        if (inputs.Data!.CanCalculate)
            calc.IsSuccess.Should().BeTrue();
        else
            calc.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotReady);
        await _repo.Received(1).HasMonthlyCostAsync(period.PeriodId, Arg.Any<CancellationToken>());
    }

    // ================= GetBreakEvenAsync UTCID645–654 =================

    [Fact]
    public async Task UTCID645_GetBreakEvenAsync_LatestRun_FourRowsSharesSum100()
    {
        // Arrange
        SeedResults(TwoRuns());

        // Act
        var result = await Sut().GetBreakEvenAsync(Month, null, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(4);
        result.Data.Items.Should().OnlyContain(i => i.CalculatedAt == T2);
        foreach (var basis in result.Data.Items.Select(i => i.AllocationBasis).Distinct())
            result.Data.Items.Where(i => i.AllocationBasis == basis).Sum(i => i.SharePct).Should().Be(100m);
        await _repo.Received(1).GetResultsAsync(10, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID646_GetBreakEvenAsync_History_ReturnsEightRows()
    {
        // Arrange
        SeedResults(TwoRuns());

        // Act
        var result = await Sut().GetBreakEvenAsync(Month, null, true, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(8);
        await _repo.Received(1).GetResultsAsync(10, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID647_GetBreakEvenAsync_ProductFilterLatest_TwoRows()
    {
        // Arrange
        SeedResults(TwoRuns());

        // Act
        var result = await Sut().GetBreakEvenAsync(Month, P1, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.Items.Should().OnlyContain(i => i.ProductId == P1 && i.CalculatedAt == T2);
        result.Data.Items.Select(i => i.AllocationBasis).Should().BeEquivalentTo("Output", "LabourHours");
        await _repo.Received(1).GetResultsAsync(10, P1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID648_GetBreakEvenAsync_ProductOnlyOnOlderRun_ReturnsThatRun()
    {
        // Arrange
        SeedResults(new[]
        {
            Row(1, P1, "P1", AllocationBasis.Output, T1, 6_000_000m),
            Row(2, P1, "P1", AllocationBasis.LabourHours, T1, 6_000_000m),
            Row(3, P2, "P2", AllocationBasis.Output, T2, 4_000_000m),
            Row(4, P2, "P2", AllocationBasis.LabourHours, T2, 4_000_000m),
        });

        // Act
        var result = await Sut().GetBreakEvenAsync(Month, P1, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
        result.Data.Items.Should().OnlyContain(i => i.ProductId == P1 && i.CalculatedAt == T1);
        await _repo.Received(1).GetResultsAsync(10, P1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID649_GetBreakEvenAsync_ZeroAllocated_SharePctZero()
    {
        // Arrange
        SeedResults(new[]
        {
            Row(1, P1, "P1", AllocationBasis.Output, T2, 0m),
            Row(2, P1, "P1", AllocationBasis.LabourHours, T2, 0m),
            Row(3, P2, "P2", AllocationBasis.Output, T2, 0m),
            Row(4, P2, "P2", AllocationBasis.LabourHours, T2, 0m),
        });

        // Act
        var result = await Sut().GetBreakEvenAsync(Month, null, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().OnlyContain(i => i.SharePct == 0m);
        await _repo.Received(1).GetResultsAsync(10, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID650_GetBreakEvenAsync_UnsortedInsert_StableProductCodeThenBasis()
    {
        // Arrange
        SeedResults(new[]
        {
            Row(1, P2, "P2", AllocationBasis.LabourHours, T2, 3_000_000m),
            Row(2, P1, "P1", AllocationBasis.Output, T2, 6_000_000m),
            Row(3, P2, "P2", AllocationBasis.Output, T2, 4_000_000m),
            Row(4, P1, "P1", AllocationBasis.LabourHours, T2, 7_000_000m),
        });

        // Act
        var result = await Sut().GetBreakEvenAsync(Month, null, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        string.Join(",", result.Data!.Items.Select(i => i.ProductCode + ":" + i.AllocationBasis))
            .Should().Be("P1:LabourHours,P1:Output,P2:LabourHours,P2:Output");
        await _repo.Received(1).GetResultsAsync(10, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID651_GetBreakEvenAsync_Month13_Validation()
    {
        // Act
        var result = await Sut().GetBreakEvenAsync("2026-13", null, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenPeriodMonthInvalid);
        await _repo.DidNotReceiveWithAnyArgs().FindPeriodByMonthAsync(default!, default);
    }

    [Fact]
    public async Task UTCID652_GetBreakEvenAsync_NoPeriod_NotFound()
    {
        // Arrange
        _repo.FindPeriodByMonthAsync("2026-08", Arg.Any<CancellationToken>()).Returns((AccountingPeriod?)null);

        // Act
        var result = await Sut().GetBreakEvenAsync("2026-08", null, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotFound);
        await _repo.DidNotReceiveWithAnyArgs().GetResultsAsync(default, default, default);
    }

    [Fact]
    public async Task UTCID653_GetBreakEvenAsync_PeriodWithoutResults_NotFound()
    {
        // Arrange
        var period = Period(month: "2026-11");
        _repo.FindPeriodByMonthAsync("2026-11", Arg.Any<CancellationToken>()).Returns(period);
        _repo.GetResultsAsync(period.PeriodId, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<BreakEvenResult>());

        // Act
        var result = await Sut().GetBreakEvenAsync("2026-11", null, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotFound);
        await _repo.Received(1).GetResultsAsync(period.PeriodId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UTCID654_GetBreakEvenAsync_NullProduct_NoExceptionSafeFailure()
    {
        // Arrange
        var period = Period();
        _repo.FindPeriodByMonthAsync(Month, Arg.Any<CancellationToken>()).Returns(period);
        _repo.GetResultsAsync(period.PeriodId, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<BreakEvenResult>
            {
                new()
                {
                    BreakEvenResultId = 1,
                    PeriodId = period.PeriodId,
                    ProductId = P1,
                    Product = null!,
                    AllocationBasis = AllocationBasis.Output,
                    CalculatedAt = Now,
                    AllocatedFixedCost = 0m,
                    IsAtRisk = false,
                },
            });

        // Act
        Exception? thrown = null;
        Result<BreakEvenListResultDto>? result = null;
        try
        {
            result = await Sut().GetBreakEvenAsync(Month, null, false, CancellationToken.None);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        // Assert
        thrown.Should().BeNull();
        result.Should().NotBeNull();
        if (result!.IsSuccess)
            result.Data!.Items.Should().BeEmpty();
        else
            result.ErrorCode.Should().NotBeNullOrEmpty();
        await _repo.Received(1).GetResultsAsync(period.PeriodId, null, Arg.Any<CancellationToken>());
    }

    // ================= ExportBreakEvenAsync UTCID655–659 =================

    [Fact]
    public async Task UTCID655_ExportBreakEvenAsync_Latest_FileNameAndFourRows()
    {
        // Arrange
        SeedResults(TwoRuns());

        // Act
        var result = await Sut().ExportBreakEvenAsync(Month, null, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be(Messages.ExcelExported);
        result.Data!.FileName.Should().Be("HoaVon_2026-10.xlsx");
        result.Data.Content.Should().NotBeEmpty();
        _writer.Received(1).WriteBreakEvenSheet(Arg.Is<IReadOnlyList<BreakEvenResultDto>>(rows => rows.Count == 4));
    }

    [Fact]
    public async Task UTCID656_ExportBreakEvenAsync_History_EightRowsSameFileName()
    {
        // Arrange
        SeedResults(TwoRuns());

        // Act
        var result = await Sut().ExportBreakEvenAsync(Month, null, true, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.FileName.Should().Be("HoaVon_2026-10.xlsx");
        _writer.Received(1).WriteBreakEvenSheet(Arg.Is<IReadOnlyList<BreakEvenResultDto>>(rows => rows.Count == 8));
    }

    [Fact]
    public async Task UTCID657_ExportBreakEvenAsync_NotCalculated_NotFoundWriterNotCalled()
    {
        // Arrange
        var period = Period(month: "2026-11");
        _repo.FindPeriodByMonthAsync("2026-11", Arg.Any<CancellationToken>()).Returns(period);
        _repo.GetResultsAsync(period.PeriodId, Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(new List<BreakEvenResult>());

        // Act
        var result = await Sut().ExportBreakEvenAsync("2026-11", null, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.NotFound);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenNotFound);
        _writer.DidNotReceiveWithAnyArgs().WriteBreakEvenSheet(default!);
    }

    [Fact]
    public async Task UTCID658_ExportBreakEvenAsync_InvalidMonth_ValidationWriterNotCalled()
    {
        // Act
        var result = await Sut().ExportBreakEvenAsync("abc", null, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Kind.Should().Be(ResultKind.Validation);
        result.ErrorCode.Should().Be(ErrorCodes.BreakEvenPeriodMonthInvalid);
        _writer.DidNotReceiveWithAnyArgs().WriteBreakEvenSheet(default!);
        await _repo.DidNotReceiveWithAnyArgs().FindPeriodByMonthAsync(default!, default);
    }

    [Fact]
    public async Task UTCID659_ExportBreakEvenAsync_WriterThrows_Propagates()
    {
        // Arrange
        SeedResults(TwoRuns());
        _writer.WriteBreakEvenSheet(Arg.Any<IReadOnlyList<BreakEvenResultDto>>())
            .Throws(new InvalidOperationException("writer"));

        // Act
        var act = async () => await Sut().ExportBreakEvenAsync(Month, null, false, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
