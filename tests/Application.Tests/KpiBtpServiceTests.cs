using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.Services;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>Màn so sánh BTP đã cắt – KPI ngày – thực tế tổ may, trừ dần BTP theo lũy kế.</summary>
public class KpiBtpServiceTests
{
    private static readonly DateOnly Start = new(2026, 10, 5);
    private static readonly DateOnly End = new(2026, 10, 9);
    private static readonly DateOnly Today = new(2026, 10, 7);   // 05, 06, 07 đã qua; 08, 09 là tương lai

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly List<KpiTarget> _targets = [];
    private readonly List<KpiDailyTarget> _plan = [];
    private readonly List<StockTransaction> _stock = [];
    private readonly List<QcBatch> _qc = [];
    private readonly List<VwKpiDailyResult> _daily = [];
    private readonly OrderLine _lineA = new() { OrderLineId = 11, OrderId = 1, ProductId = 1 };
    private readonly OrderLine _lineB = new() { OrderLineId = 12, OrderId = 1, ProductId = 1 };
    private readonly OrderLine _otherProduct = new() { OrderLineId = 21, OrderId = 1, ProductId = 2 };
    private readonly OrderLine _otherOrder = new() { OrderLineId = 31, OrderId = 2, ProductId = 1 };

    public KpiBtpServiceTests()
    {
        _clock.Today.Returns(Today);
        Setup(_targets); Setup(_plan); Setup(_stock); Setup(_qc); Setup(_daily);
        _targets.Add(Kpi(1, team: 5));
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private KpiBtpService Service() => new(_uow, new OutputQueryService(_uow), _clock);

    private static KpiTarget Kpi(int id, int team, int total = 300, short days = 5, string status = KpiStatuses.Active) => new()
    {
        KpiTargetId = id, OrderId = 1, ProductId = 1, SewingTeamId = team, StartDate = Start, EndDate = End, TargetFinishedQty = total,
        WorkingDays = days, Status = status, Order = new CustomerOrder { OrderId = 1, OrderCode = "DH001" },
        Product = new Product { ProductId = 1, ProductCode = "MH001", ProductName = "Áo" },
        SewingTeam = new Team { TeamId = team, TeamName = $"Tổ may {team}" },
    };

    private void Plan(params int[] qty)
    {
        for (var i = 0; i < qty.Length; i++)
            _plan.Add(new KpiDailyTarget { KpiTargetId = 1, WorkDate = Start.AddDays(i), TargetQty = qty[i] });
    }

    private void Cut(OrderLine line, DateOnly date, int qty) => _stock.Add(new StockTransaction
    {
        StockTransactionId = _stock.Count + 1, WarehouseType = WarehouseTypes.SemiFinished, SourceDocType = StockDocTypes.CutHandover,
        OrderLineId = line.OrderLineId, OrderLine = line, TransactionDate = date, QtyChange = qty,
    });

    private void Done(DateOnly date, int qty) => _daily.Add(new VwKpiDailyResult
    {
        KpiTargetId = 1, SewingTeamId = 5, OrderId = 1, ProductId = 1, WorkDate = date, FinishedQty = qty,
    });

    [Fact]
    public async Task Cumulative_cut_actual_and_remaining_are_subtracted_day_by_day()
    {
        Plan(30, 50, 70, 70, 80);
        Cut(_lineA, Start, 100); Cut(_lineB, Start, 60); Cut(_lineA, Start.AddDays(2), 80);
        Done(Start, 20); Done(Start.AddDays(1), 45); Done(Start.AddDays(2), 70);

        var result = await Service().GetAsync(1, null, default);

        result.IsSuccess.Should().BeTrue();
        var d = result.Data!.Days;
        d.Select(x => x.WorkDate).Should().Equal(Start, Start.AddDays(1), Start.AddDays(2), Start.AddDays(3), Start.AddDays(4));
        d.Select(x => x.CumTarget).Should().Equal(30m, 80m, 150m, 220m, 300m);
        d.Select(x => x.CumCut).Should().Equal(160, 160, 240, 240, 240);
        d.Take(3).Select(x => x.ActualQty).Should().Equal(20, 45, 70);
        d.Take(3).Select(x => x.CumActual).Should().Equal(20, 65, 135);
        d.Take(3).Select(x => x.BtpRemaining).Should().Equal(140m, 95m, 105m);
        result.Data.Summary.CutTotal.Should().Be(240);
        result.Data.Summary.ActualTotal.Should().Be(135);
        result.Data.Summary.BtpRemainingNow.Should().Be(105m);
        result.Data.Summary.PlanTotal.Should().Be(300);
        result.Data.Summary.CutShortfall.Should().Be(60m);   // KPI 300 − BTP đã cắt 240
    }

    [Fact]
    public async Task Future_days_have_no_actual_and_the_remaining_btp_is_projected_from_the_plan()
    {
        Plan(30, 50, 70, 70, 80);
        Cut(_lineA, Start, 300);
        Done(Start, 30); Done(Start.AddDays(1), 50); Done(Start.AddDays(2), 70);

        var d = (await Service().GetAsync(1, null, default)).Data!.Days;

        d[3].IsFuture.Should().BeTrue();
        d[3].ActualQty.Should().BeNull();
        d[2].BtpRemaining.Should().Be(150m);
        d[3].BtpRemaining.Should().Be(80m);    // 150 − KPI ngày 08
        d[4].BtpRemaining.Should().Be(0m);     // 80 − KPI ngày 09
        d[3].LowBtp.Should().BeFalse();        // ngày tương lai không báo động
        d[4].BehindTarget.Should().BeFalse();
    }

    [Fact]
    public async Task Low_btp_is_flagged_when_what_is_left_cannot_cover_the_next_days_target()
    {
        Plan(30, 50, 70, 70, 80);
        Cut(_lineA, Start, 120);
        Done(Start, 30); Done(Start.AddDays(1), 50); Done(Start.AddDays(2), 30);   // còn 10, ngày mai cần 70

        var d = (await Service().GetAsync(1, null, default)).Data!.Days;

        d[2].BtpRemaining.Should().Be(10m);
        d[2].LowBtp.Should().BeTrue();
        d[0].LowBtp.Should().BeFalse();        // còn 90 ≥ 50
        d[2].BehindTarget.Should().BeTrue();   // lũy kế 110 < 150
        d[0].BehindTarget.Should().BeFalse();
    }

    [Fact]
    public async Task Btp_cut_and_made_before_the_period_carry_over_as_the_opening_balance()
    {
        Plan(30, 50, 70, 70, 80);
        Cut(_lineA, Start.AddDays(-3), 200);    // trước kỳ
        _qc.Add(new QcBatch { QcBatchId = 1, SewingTeamId = 5, OrderLine = _lineA, OrderLineId = 11, WorkDate = Start.AddDays(-2), FinishedQty = 80 });
        _qc.Add(new QcBatch { QcBatchId = 2, SewingTeamId = 9, OrderLine = _lineA, OrderLineId = 11, WorkDate = Start.AddDays(-2), FinishedQty = 500 });   // tổ khác
        Done(Start, 30);

        var r = (await Service().GetAsync(1, null, default)).Data!;

        r.Summary.OpeningBtp.Should().Be(120);
        r.Days[0].BtpRemaining.Should().Be(90m);   // 120 + 0 − 30
        r.Days[0].CumCut.Should().Be(200);
        r.Days[0].CumActual.Should().Be(110);
        r.Summary.CutTotal.Should().Be(0);         // chỉ cắt trong kỳ
        r.Summary.CutShortfall.Should().Be(180m);  // 300 − (120 + 0)
    }

    [Fact]
    public async Task Only_cut_handovers_of_the_same_order_and_product_count()
    {
        Plan(30, 50, 70, 70, 80);
        Cut(_lineA, Start, 100);
        Cut(_otherProduct, Start, 999);
        Cut(_otherOrder, Start, 999);
        _stock.Add(new StockTransaction   // không phải CUT_HANDOVER và không phải kho BTP
        {
            StockTransactionId = 99, WarehouseType = WarehouseTypes.SemiFinished, SourceDocType = StockDocTypes.MaterialIssue,
            OrderLineId = 11, OrderLine = _lineA, TransactionDate = Start, QtyChange = 77,
        });
        _stock.Add(new StockTransaction
        {
            StockTransactionId = 100, WarehouseType = WarehouseTypes.Finished, SourceDocType = StockDocTypes.CutHandover,
            OrderLineId = 11, OrderLine = _lineA, TransactionDate = Start, QtyChange = 55,
        });
        Done(Start, 10);

        var r = (await Service().GetAsync(1, null, default)).Data!;

        r.Summary.CutTotal.Should().Be(100);
        r.Days[0].CutQty.Should().Be(100);
    }

    [Fact]
    public async Task A_kpi_without_daily_targets_falls_back_to_an_even_split()
    {
        // 300 chia 5 ngày = 60/ngày; không có dòng chỉ tiêu ngày
        Cut(_lineA, Start, 200);
        Done(Start, 50); Done(Start.AddDays(1), 70);

        var d = (await Service().GetAsync(1, null, default)).Data!.Days;

        d.Select(x => x.DailyTarget).Distinct().Should().Equal(60m);
        d.Select(x => x.WorkDate).Should().Equal(Start, Start.AddDays(1));   // chỉ ngày có cắt / thực tế
        d[1].CumTarget.Should().Be(120m);
    }

    [Fact]
    public async Task Days_with_cut_but_no_target_or_output_still_appear()
    {
        Plan(30, 50);
        Cut(_lineA, Start.AddDays(3), 40);   // 08/10: có cắt, không có KPI, tương lai

        var d = (await Service().GetAsync(1, null, default)).Data!.Days;

        d.Select(x => x.WorkDate).Should().Equal(Start, Start.AddDays(1), Start.AddDays(3));
        d[2].DailyTarget.Should().Be(0m);
        d[2].CutQty.Should().Be(40);
    }

    [Fact]
    public async Task A_team_leader_only_sees_the_kpi_of_their_own_team()
    {
        Plan(30);
        _targets.Add(Kpi(2, team: 6));

        (await Service().GetAsync(1, 5, default)).IsSuccess.Should().BeTrue();
        var other = await Service().GetAsync(1, 6, default);
        other.IsSuccess.Should().BeFalse();
        other.Kind.Should().Be(ResultKind.NotFound);

        (await Service().GetOptionsAsync(5, default)).Data!.Select(o => o.KpiTargetId).Should().Equal(1);
        (await Service().GetOptionsAsync(6, default)).Data!.Select(o => o.KpiTargetId).Should().Equal(2);
        (await Service().GetOptionsAsync(null, default)).Data!.Select(o => o.KpiTargetId).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task Options_hide_cancelled_kpis_and_a_missing_kpi_is_not_found()
    {
        _targets.Add(Kpi(3, team: 5, status: KpiStatuses.Cancelled));
        _targets.Add(Kpi(4, team: 5, status: KpiStatuses.Closed));

        var options = (await Service().GetOptionsAsync(null, default)).Data!;

        options.Select(o => o.KpiTargetId).Should().BeEquivalentTo(new[] { 1, 4 });
        options.Single(o => o.KpiTargetId == 4).Status.Should().Be("Closed");
        (await Service().GetAsync(404, null, default)).Kind.Should().Be(ResultKind.NotFound);
    }
}
