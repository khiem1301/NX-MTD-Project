using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>KI-10: IOutputQueryService — nguồn đọc dùng chung cho lương tạm tính, KPI, tiến độ, bàn giao thành phẩm.</summary>
public class OutputQueryServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly List<VwWorkerDailyEstimatedPay> _pay = [];
    private readonly List<VwKpiDailyResult> _kpi = [];
    private readonly List<VwOrderLineProgress> _progress = [];
    private readonly List<QcBatch> _qc = [];
    private readonly OutputQueryService _sut;

    public OutputQueryServiceTests()
    {
        Setup(_pay);
        Setup(_kpi);
        Setup(_progress);
        Setup(_qc);
        _sut = new OutputQueryService(_uow);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private static VwKpiDailyResult Kpi(int id, int day, int? qty) => new()
    {
        KpiTargetId = id, SewingTeamId = 1, OrderId = 1, ProductId = 1, WorkDate = new DateOnly(2026, 10, day), FinishedQty = qty,
    };

    // ---------- Lương tạm tính ----------

    [Fact]
    public async Task WorkerDailyPay_FiltersWorkerAndRangeAndOrdersByDate()
    {
        VwWorkerDailyEstimatedPay Day(int worker, int day, decimal pay) => new()
        {
            WorkerId = worker, WorkDate = new DateOnly(2026, 10, day), OutputQty = 10, OutputAmount = pay, EstimatedPay = pay,
            SundayBonus = null, IsSunday = null,
        };
        _pay.Add(Day(1, 20, 200m));
        _pay.Add(Day(1, 5, 50m));
        _pay.Add(Day(2, 6, 999m));   // người khác
        _pay.Add(Day(1, 28, 280m));  // ngoài khoảng

        var days = await _sut.GetWorkerDailyPayAsync(1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 25), default);

        days.Select(d => d.EstimatedPay).Should().Equal(50m, 200m);
        days[0].IsSunday.Should().BeFalse();   // view trả NULL → false
        days[0].SundayBonus.Should().Be(0m);
    }

    // ---------- KPI ----------

    [Fact]
    public async Task KpiFinishedTotals_SumsPerTarget_TreatsNullAsZero_AndSkipsTargetsWithoutQc()
    {
        _kpi.Add(Kpi(1, 5, 100));
        _kpi.Add(Kpi(1, 6, 150));
        _kpi.Add(Kpi(2, 5, null));
        _kpi.Add(Kpi(3, 5, 999));   // không nằm trong danh sách hỏi

        var totals = await _sut.GetKpiFinishedTotalsAsync([1, 2, 4], default);

        totals[1].Should().Be(250);
        totals[2].Should().Be(0);
        totals.ContainsKey(3).Should().BeFalse();
        totals.ContainsKey(4).Should().BeFalse();
    }

    [Fact]
    public async Task KpiFinishedTotals_EmptyInput_ReturnsEmptyWithoutQuerying()
    {
        (await _sut.GetKpiFinishedTotalsAsync([], default)).Should().BeEmpty();
    }

    [Fact]
    public async Task KpiDailyResults_OnlyThatTargetOrderedByDate()
    {
        _kpi.Add(Kpi(1, 9, 30));
        _kpi.Add(Kpi(1, 3, 10));
        _kpi.Add(Kpi(2, 4, 99));

        var rows = await _sut.GetKpiDailyResultsAsync(1, default);

        rows.Select(r => r.WorkDate.Day).Should().Equal(3, 9);
        rows.Should().OnlyContain(r => r.KpiTargetId == 1);
    }

    // ---------- Tiến độ ----------

    [Fact]
    public async Task OrderLineProgress_ReturnsOnlyThatOrderSortedByProductColorSize()
    {
        VwOrderLineProgress Line(int id, int order, string product, string color, string size) => new()
        {
            OrderLineId = id, OrderId = order, OrderCode = "DH00" + order, ProductCode = product, Color = color, SizeCode = size,
            OrderedQty = 100, FinishedQty = 40, ScrapQty = 2, DefectRatePct = 4.76m,
        };
        _progress.Add(Line(3, 1, "MH002", "Đen", "M"));
        _progress.Add(Line(1, 1, "MH001", "Trắng", "S"));
        _progress.Add(Line(2, 1, "MH001", "Trắng", "L"));
        _progress.Add(Line(9, 2, "MH001", "Đen", "M"));

        var rows = await _sut.GetOrderLineProgressAsync(1, default);

        rows.Select(r => r.OrderLineId).Should().Equal(2, 1, 3);
        rows[0].FinishedQty.Should().Be(40);
        rows[0].DefectRatePct.Should().Be(4.76m);
    }

    // ---------- Số QC đạt cho bàn giao thành phẩm ----------

    [Fact]
    public async Task QcFinishedByOrderLine_SumsAllBatchesOfEachLine()
    {
        _qc.Add(new QcBatch { OrderLineId = 11, FinishedQty = 100 });
        _qc.Add(new QcBatch { OrderLineId = 11, FinishedQty = 50 });
        _qc.Add(new QcBatch { OrderLineId = 12, FinishedQty = 70 });
        _qc.Add(new QcBatch { OrderLineId = 13, FinishedQty = 999 });

        var totals = await _sut.GetQcFinishedByOrderLineAsync([11, 12], default);

        totals[11].Should().Be(150);
        totals[12].Should().Be(70);
        totals.ContainsKey(13).Should().BeFalse();
    }
}
