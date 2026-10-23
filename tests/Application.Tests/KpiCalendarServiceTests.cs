using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// Lịch KPI theo ngày: Tổng BTP (cắt vào kho − ra chuyền), số còn lại để đặt KPI, thực tế của tổ, và lưu KPI từng ngày
/// (chặn khi vượt tổng cả kỳ hoặc vượt BTP còn lại).
/// </summary>
public class KpiCalendarServiceTests
{
    private static readonly DateOnly Start = new(2026, 10, 5);
    private static readonly DateOnly End = new(2026, 10, 12);
    private static readonly DateOnly Today = new(2026, 10, 7);   // 05, 06 đã qua; 07 hôm nay; 08..12 tương lai

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly List<KpiTarget> _targets = [];
    private readonly List<KpiDailyTarget> _plan = [];
    private readonly List<StockTransaction> _stock = [];
    private readonly List<QcBatch> _qc = [];
    private readonly OrderLine _lineA = new() { OrderLineId = 11, OrderId = 1, ProductId = 1 };
    private readonly OrderLine _lineB = new() { OrderLineId = 12, OrderId = 1, ProductId = 1 };
    private readonly OrderLine _otherProduct = new() { OrderLineId = 21, OrderId = 1, ProductId = 2 };
    private readonly OrderLine _otherOrder = new() { OrderLineId = 31, OrderId = 2, ProductId = 1 };

    public KpiCalendarServiceTests()
    {
        _clock.Today.Returns(Today);
        Setup(_targets); Setup(_plan); Setup(_stock); Setup(_qc);
        _uow.Repository<KpiDailyTarget>().AddAsync(Arg.Do<KpiDailyTarget>(d => _plan.Add(d))).Returns(Task.CompletedTask);
        _uow.Repository<KpiDailyTarget>().When(r => r.RemoveRange(Arg.Any<IEnumerable<KpiDailyTarget>>()))
            .Do(c => { foreach (var d in c.Arg<IEnumerable<KpiDailyTarget>>().ToList()) _plan.Remove(d); });
        _uow.SaveChangesAsync().Returns(Task.FromResult(1));
        _targets.Add(Kpi(1, team: 5));
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private KpiCalendarService Service() => new(_uow, _clock, _audit);

    private static KpiTarget Kpi(int id, int team, int total = 400, string status = KpiStatuses.Active, int productId = 1) => new()
    {
        KpiTargetId = id, OrderId = 1, ProductId = productId, SewingTeamId = team, StartDate = Start, EndDate = End, TargetFinishedQty = total,
        WorkingDays = 7, Status = status, Order = new CustomerOrder { OrderId = 1, OrderCode = "DH001" },
        Product = new Product { ProductId = productId, ProductCode = "MH001", ProductName = "Áo" },
        SewingTeam = new Team { TeamId = team, TeamName = $"Tổ may {team}" },
    };

    private void Plan(int kpi, params (int dayOffset, int qty)[] rows)
    {
        foreach (var (o, q) in rows) _plan.Add(new KpiDailyTarget { KpiTargetId = kpi, WorkDate = Start.AddDays(o), TargetQty = q });
    }

    private void Cut(OrderLine line, DateOnly date, int qty) => _stock.Add(new StockTransaction
    {
        StockTransactionId = _stock.Count + 1, WarehouseType = WarehouseTypes.SemiFinished, SourceDocType = StockDocTypes.CutHandover,
        OrderLineId = line.OrderLineId, OrderLine = line, TransactionDate = date, QtyChange = qty,
    });

    private void Out(int team, DateOnly date, int qty, OrderLine? line = null) => _qc.Add(new QcBatch
    {
        QcBatchId = _qc.Count + 1, SewingTeamId = team, OrderLine = line ?? _lineA, OrderLineId = (line ?? _lineA).OrderLineId, WorkDate = date, FinishedQty = qty,
    });

    private static SaveKpiDailyPlanDto Save(params (int dayOffset, int qty)[] rows) =>
        new(rows.Select(r => new KpiDailyTargetInputDto(Start.AddDays(r.dayOffset), r.qty)).ToList());

    /// <summary>Cắt 300 (05/10, hai dòng đơn) → Tổng BTP = 300 − ra chuyền 120 (tổ 5: 40 + 60, tổ 9: 20) = 180.</summary>
    private void Warehouse()
    {
        Cut(_lineA, Start, 200); Cut(_lineB, Start, 100);
        Cut(_lineA, Start.AddDays(3), 100);   // 08/10: tương lai, chưa tính vào Tổng BTP
        Out(5, Start, 40); Out(5, Start.AddDays(1), 60); Out(9, Start, 20);
    }

    // ---------- Tổng BTP và số còn lại để đặt KPI ----------

    [Fact]
    public async Task Total_btp_is_cut_into_the_warehouse_minus_what_all_teams_sent_to_the_line()
    {
        Warehouse();

        var s = (await Service().GetAsync(1, default)).Data!.Summary;

        s.CutTotal.Should().Be(300);
        s.OutTotal.Should().Be(120);
        s.TotalBtp.Should().Be(180);
        s.CommittedAll.Should().Be(0);
        s.RemainingToSet.Should().Be(180);
    }

    [Fact]
    public async Task Total_btp_ignores_other_products_other_orders_and_other_warehouse_documents()
    {
        Warehouse();
        Cut(_otherProduct, Start, 999); Cut(_otherOrder, Start, 999);
        _stock.Add(new StockTransaction   // không phải bàn giao cắt
        {
            StockTransactionId = 90, WarehouseType = WarehouseTypes.SemiFinished, SourceDocType = StockDocTypes.MaterialIssue,
            OrderLineId = 11, OrderLine = _lineA, TransactionDate = Start, QtyChange = 77,
        });
        _stock.Add(new StockTransaction   // kho thành phẩm
        {
            StockTransactionId = 91, WarehouseType = WarehouseTypes.Finished, SourceDocType = StockDocTypes.CutHandover,
            OrderLineId = 11, OrderLine = _lineA, TransactionDate = Start, QtyChange = 55,
        });
        Out(5, Start, 500, _otherProduct);

        (await Service().GetAsync(1, default)).Data!.Summary.TotalBtp.Should().Be(180);
    }

    [Fact]
    public async Task Committed_kpi_is_the_future_days_plus_what_todays_kpi_still_lacks_across_all_active_kpis_of_the_same_order_and_product()
    {
        Warehouse();
        Out(5, Today, 25);                                     // hôm nay tổ 5 đã ra chuyền 25 → Tổng BTP = 155
        Plan(1, (1, 50), (2, 60), (3, 70), (4, 80));           // 06/10 (đã qua, không tính), 07/10 hôm nay, 08, 09
        _targets.Add(Kpi(2, team: 9));
        Plan(2, (3, 30));                                      // KPI khác cùng đơn + mã hàng: 08/10
        _targets.Add(Kpi(3, team: 7, productId: 2));
        Plan(3, (3, 999));                                     // mã hàng khác: không tính
        _targets.Add(Kpi(4, team: 8, status: KpiStatuses.Closed));
        Plan(4, (3, 999));                                     // đã đóng: không giữ chỗ

        var s = (await Service().GetAsync(1, default)).Data!.Summary;

        s.TotalBtp.Should().Be(155);
        s.CommittedThis.Should().Be(35 + 70 + 80);             // hôm nay còn thiếu 60 − 25 = 35
        s.CommittedAll.Should().Be(185 + 30);
        s.RemainingToSet.Should().Be(155 - 215);               // âm = đã đặt vượt BTP
    }

    [Fact]
    public async Task Every_day_of_the_period_is_listed_with_target_actual_cut_and_the_remaining_btp()
    {
        Warehouse();
        Plan(1, (0, 30), (1, 50), (2, 60), (4, 40));
        Out(5, Today, 25);

        var r = (await Service().GetAsync(1, default)).Data!;

        r.Days.Select(d => d.WorkDate).Should().Equal(Enumerable.Range(0, 8).Select(i => Start.AddDays(i)));
        r.Days.Select(d => d.TargetQty).Should().Equal(30, 50, 60, 0, 40, 0, 0, 0);
        r.Days.Select(d => d.CumTarget).Should().Equal(30, 80, 140, 140, 180, 180, 180, 180);
        r.Days.Take(3).Select(d => d.ActualQty).Should().Equal(40, 60, 25);
        r.Days.Skip(3).Should().OnlyContain(d => d.ActualQty == null && d.IsFuture);
        r.Days[2].IsToday.Should().BeTrue();
        r.Days[0].CutQty.Should().Be(300);
        r.Days[3].CutQty.Should().Be(100);
        // Số thật: cắt lũy kế − ra chuyền lũy kế của mọi tổ
        r.Days[0].BtpRemaining.Should().Be(300 - 60);
        r.Days[1].BtpRemaining.Should().Be(300 - 120);
        r.Days[2].BtpRemaining.Should().Be(300 - 145);
    }

    [Fact]
    public async Task Future_days_project_the_remaining_btp_from_everything_already_planned()
    {
        Warehouse();
        Out(5, Today, 25);                    // Tổng BTP hôm nay = 155
        Plan(1, (2, 60), (3, 70), (4, 80));   // hôm nay 60 (đã ra chuyền 25 → còn thiếu 35), 08: 70, 09: 80
        _targets.Add(Kpi(2, team: 9));
        Plan(2, (3, 30));                     // tổ khác 08/10: 30

        var d = (await Service().GetAsync(1, default)).Data!.Days;

        d[2].BtpRemaining.Should().Be(155);                       // hôm nay: số thật
        d[3].BtpRemaining.Should().Be(155 - 35 + 100 - 70 - 30);  // − phần hôm nay còn thiếu + cắt 08/10 − KPI hai tổ ngày 08
        d[4].BtpRemaining.Should().Be(d[3].BtpRemaining - 80);
    }

    [Fact]
    public async Task Warnings_flag_low_btp_and_a_team_that_fell_behind_but_never_the_future()
    {
        Cut(_lineA, Start, 100);
        Out(5, Start, 30); Out(5, Start.AddDays(1), 40);      // còn 30 sau 06/10, ngày kế tiếp cần 50
        Plan(1, (0, 30), (1, 60), (2, 50), (3, 50));

        var d = (await Service().GetAsync(1, default)).Data!.Days;

        d[1].BtpRemaining.Should().Be(30);
        d[1].LowBtp.Should().BeTrue();          // 30 < KPI ngày kế tiếp (50)
        d[0].LowBtp.Should().BeFalse();         // còn 70 ≥ 60
        d[1].BehindTarget.Should().BeTrue();    // lũy kế thực tế 70 < KPI lũy kế 90
        d[0].BehindTarget.Should().BeFalse();
        d[2].BehindTarget.Should().BeFalse();   // hôm nay chưa chốt
        d.Skip(3).Should().OnlyContain(x => !x.LowBtp && !x.BehindTarget);
    }

    [Fact]
    public async Task Summary_shows_what_is_planned_and_what_is_left_of_the_period_total()
    {
        Plan(1, (0, 100), (1, 120));

        var s = (await Service().GetAsync(1, default)).Data!.Summary;

        s.PlanTotal.Should().Be(400);
        s.Allocated.Should().Be(220);
        s.Unallocated.Should().Be(180);
        s.CanEdit.Should().BeTrue();
        s.Today.Should().Be(Today);
    }

    [Fact]
    public async Task A_missing_kpi_is_not_found_and_a_closed_kpi_is_read_only()
    {
        (await Service().GetAsync(404, default)).Kind.Should().Be(ResultKind.NotFound);
        _targets.Add(Kpi(5, team: 5, status: KpiStatuses.Closed));

        var closed = await Service().GetAsync(5, default);

        closed.Data!.Summary.CanEdit.Should().BeFalse();
    }

    // ---------- Lưu KPI từng ngày ----------

    [Fact]
    public async Task Saving_replaces_every_day_updates_the_working_days_and_writes_an_audit_entry()
    {
        Warehouse();
        Plan(1, (0, 10), (1, 10));

        var result = await Service().SaveDailyPlanAsync(1, Save((2, 40), (3, 50), (4, 60)), default);

        result.IsSuccess.Should().BeTrue();
        _plan.Where(p => p.KpiTargetId == 1).Select(p => (p.WorkDate, p.TargetQty)).Should().Equal(
            (Start.AddDays(2), 40), (Start.AddDays(3), 50), (Start.AddDays(4), 60));
        _targets.Single().WorkingDays.Should().Be(3);
        result.Data!.Days.Select(d => d.TargetQty).Should().Equal(0, 0, 40, 50, 60, 0, 0, 0);
        _audit.Received(1).Add(Arg.Is<AuditEntry>(e => e.TableName == nameof(KpiTarget) && e.RecordId == 1 && e.Action == AuditActions.Update));
    }

    [Fact]
    public async Task Saving_leaves_the_days_of_other_kpis_alone()
    {
        Warehouse();
        _targets.Add(Kpi(2, team: 9));
        Plan(2, (3, 30));

        (await Service().SaveDailyPlanAsync(1, Save((2, 20)), default)).IsSuccess.Should().BeTrue();

        _plan.Should().Contain(p => p.KpiTargetId == 2 && p.TargetQty == 30);
    }

    [Fact]
    public async Task An_empty_plan_clears_all_days_and_falls_back_to_the_period_without_sundays()
    {
        Warehouse();
        Plan(1, (0, 10), (1, 10));

        var result = await Service().SaveDailyPlanAsync(1, Save(), default);

        result.IsSuccess.Should().BeTrue();
        _plan.Should().BeEmpty();
        _targets.Single().WorkingDays.Should().Be(7);   // 05..12/10/2026 trừ Chủ nhật 11/10
    }

    [Fact]
    public async Task The_days_cannot_add_up_to_more_than_the_period_total()
    {
        Warehouse();

        var result = await Service().SaveDailyPlanAsync(1, Save((3, 150), (4, 150), (5, 150)), default);   // 450 > 400

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.KpiPlanInvalid);
        result.Message.Should().Contain("450").And.Contain("400");
        _plan.Should().BeEmpty();
    }

    [Fact]
    public async Task Planning_more_than_the_remaining_btp_is_refused_and_says_how_much_is_left()
    {
        Warehouse();                                              // Tổng BTP = 180
        _targets.Add(Kpi(2, team: 9));
        Plan(2, (3, 30));                                         // KPI khác đã giữ 30 → KPI này còn tối đa 150

        var tooMuch = await Service().SaveDailyPlanAsync(1, Save((3, 100), (4, 51)), default);   // 151
        var fits = await Service().SaveDailyPlanAsync(1, Save((3, 100), (4, 50)), default);      // 150

        tooMuch.IsSuccess.Should().BeFalse();
        tooMuch.ErrorCode.Should().Be(ErrorCodes.KpiPlanInvalid);
        tooMuch.Message.Should().Contain("150");
        fits.IsSuccess.Should().BeTrue();
        fits.Data!.Summary.RemainingToSet.Should().Be(0);
    }

    [Fact]
    public async Task Only_the_not_yet_produced_part_of_todays_target_counts_against_the_btp()
    {
        Warehouse();
        Out(5, Today, 55);                                        // Tổng BTP = 125; hôm nay đã ra chuyền 55

        var result = await Service().SaveDailyPlanAsync(1, Save((2, 60), (3, 120)), default);   // còn thiếu 5 + 120 = 125

        result.IsSuccess.Should().BeTrue();
        result.Data!.Summary.CommittedThis.Should().Be(125);
        result.Data.Summary.RemainingToSet.Should().Be(0);
    }

    [Fact]
    public async Task Past_days_can_be_changed_without_using_up_any_btp()
    {
        Warehouse();   // Tổng BTP = 180

        var result = await Service().SaveDailyPlanAsync(1, Save((0, 300), (1, 20)), default);   // 05, 06/10 đã qua

        result.IsSuccess.Should().BeTrue();
        result.Data!.Summary.CommittedThis.Should().Be(0);
    }

    [Fact]
    public async Task A_plan_that_is_already_over_the_btp_can_still_be_lowered_but_not_raised()
    {
        Warehouse();                                              // Tổng BTP = 180
        Plan(1, (3, 200), (4, 100));                              // đã đặt 300 > 180 từ trước (BTP bị ra chuyền nhiều hơn dự kiến)

        var lower = await Service().SaveDailyPlanAsync(1, Save((3, 150), (4, 100)), default);
        var raise = await Service().SaveDailyPlanAsync(1, Save((3, 150), (4, 150)), default);

        lower.IsSuccess.Should().BeTrue();
        raise.IsSuccess.Should().BeFalse();
        raise.ErrorCode.Should().Be(ErrorCodes.KpiPlanInvalid);
        _plan.Where(p => p.KpiTargetId == 1).Select(p => p.TargetQty).Should().Equal(150, 100);
    }

    [Fact]
    public async Task Days_outside_the_period_are_refused()
    {
        Warehouse();

        var before = await Service().SaveDailyPlanAsync(1, Save((-1, 10)), default);
        var after = await Service().SaveDailyPlanAsync(1, Save((8, 10)), default);

        before.IsSuccess.Should().BeFalse();
        before.Kind.Should().Be(ResultKind.Validation);
        after.IsSuccess.Should().BeFalse();
        _plan.Should().BeEmpty();
    }

    [Fact]
    public async Task Only_an_active_kpi_can_be_planned_and_a_missing_one_is_not_found()
    {
        _targets.Add(Kpi(5, team: 5, status: KpiStatuses.Closed));
        _targets.Add(Kpi(6, team: 5, status: KpiStatuses.Cancelled));

        (await Service().SaveDailyPlanAsync(5, Save((3, 10)), default)).ErrorCode.Should().Be(ErrorCodes.KpiInvalidStatus);
        (await Service().SaveDailyPlanAsync(6, Save((3, 10)), default)).ErrorCode.Should().Be(ErrorCodes.KpiInvalidStatus);
        (await Service().SaveDailyPlanAsync(404, Save((3, 10)), default)).Kind.Should().Be(ResultKind.NotFound);
    }
}
