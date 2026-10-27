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

/// <summary>KH-16 / KH-23, UC21: dashboard tiến độ đơn, JOB-04 nguy cơ trễ, JOB-05 xóa thông báo cũ.</summary>
public class OrderProgressServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 10);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IOutputQueryService _output = Substitute.For<IOutputQueryService>();
    private readonly ISystemSettingReader _settings = Substitute.For<ISystemSettingReader>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();

    private readonly List<CustomerOrder> _orders = new();
    private readonly List<OrderLine> _lines = new();
    private readonly List<QcBatch> _qc = new();
    private readonly List<Notification> _notifications = new();
    private readonly List<VwOrderLineProgress> _view = new();

    public OrderProgressServiceTests()
    {
        _settings.GetIntAsync(SettingKeys.DelayAvgDays, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(7);
        _settings.GetIntAsync(SettingKeys.NotificationRetentionDays, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(90);
        Setup(_orders); Setup(_lines); Setup(_qc); Setup(_notifications); Setup(_view);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        repo.When(r => r.RemoveRange(Arg.Any<IEnumerable<T>>())).Do(c => { foreach (var x in c.Arg<IEnumerable<T>>().ToList()) backing.Remove(x); });
        _uow.Repository<T>().Returns(repo);
    }

    private OrderProgressService Service() => new(_uow, _output, _settings, _notifier, NullLogger<OrderProgressService>.Instance);

    private CustomerOrder Order(int id, int dueInDays, int ordered, int finished, int finishedLast7Days, string status = OrderStatuses.InProduction)
    {
        var o = new CustomerOrder { OrderId = id, OrderCode = $"DH-{id}", Status = status, MaterialStatus = OrderMaterialStatuses.Complete, DueDate = Today.AddDays(dueInDays), Customer = new Customer { CustomerName = "Khách" } };
        _orders.Add(o);
        var line = new OrderLine { OrderLineId = id * 10, OrderId = id, Order = o, OrderedQty = ordered };
        _lines.Add(line);
        if (finished - finishedLast7Days > 0)
            _qc.Add(new QcBatch { QcBatchId = _qc.Count + 1, OrderLineId = line.OrderLineId, OrderLine = line, WorkDate = Today.AddDays(-20), FinishedQty = finished - finishedLast7Days });
        if (finishedLast7Days > 0)
            _qc.Add(new QcBatch { QcBatchId = _qc.Count + 1, OrderLineId = line.OrderLineId, OrderLine = line, WorkDate = Today.AddDays(-2), FinishedQty = finishedLast7Days });
        return o;
    }

    // ---------- Công thức nguy cơ trễ ----------

    [Theory]
    [InlineData(1000, 700, 70, 7, 30, false)]   // 300 / 10 = 30 ngày, còn đúng 30 ngày → kịp
    [InlineData(1000, 700, 70, 7, 29, true)]    // thiếu 1 ngày → trễ
    [InlineData(1000, 700, 70, 7, 20, true)]    // 300 / 10 = 30 ngày > 20 ngày còn lại
    [InlineData(1000, 1000, 70, 7, -5, false)]  // đã xong thì không có nguy cơ dù quá hạn
    [InlineData(1000, 700, 70, 7, 0, true)]     // còn hàng mà hôm nay là hạn
    [InlineData(1000, 100, 0, 7, 20, false)]    // 7 ngày qua chưa làm gì nhưng còn hạn → chưa coi là nguy cơ
    [InlineData(1000, 100, 0, 7, -1, true)]     // chưa làm gì và đã quá hạn → nguy cơ
    public void Formula_matches_the_remaining_over_average_rule(int ordered, int finished, int window, int avg, int daysLeft, bool expected) =>
        OrderProgressService.IsDelayRisk(ordered, finished, window, avg, daysLeft).Should().Be(expected);

    // ---------- JOB-04 ----------

    [Fact]
    public async Task Delay_check_flags_a_late_order_once_and_notifies_admin_and_manager()
    {
        var late = Order(1, dueInDays: 10, ordered: 1000, finished: 500, finishedLast7Days: 70);   // 500/10 = 50 ngày > 10

        var flagged = await Service().RunDelayCheckAsync(Today, default);

        flagged.Should().Be(1);
        late.DelayRiskSince.Should().Be(Today);   // IsDelayRisk là cột tính từ DelayRiskSince
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Admin, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());

        _notifier.ClearReceivedCalls();
        (await Service().RunDelayCheckAsync(Today.AddDays(1), default)).Should().Be(0);   // đã có cờ: không báo lại
        late.DelayRiskSince.Should().Be(Today);
        await _notifier.DidNotReceive().NotifyRoleAsync(Arg.Any<string>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delay_check_clears_the_flag_when_the_order_catches_up()
    {
        var o = Order(1, dueInDays: 30, ordered: 1000, finished: 900, finishedLast7Days: 700);   // 100 / 100 = 1 ngày < 30
        o.DelayRiskSince = Today.AddDays(-3);

        await Service().RunDelayCheckAsync(Today, default);

        o.DelayRiskSince.Should().BeNull();
    }

    [Fact]
    public async Task Delay_check_only_looks_at_orders_in_production()
    {
        var waiting = Order(1, dueInDays: -5, ordered: 1000, finished: 0, finishedLast7Days: 0, status: OrderStatuses.New);
        var running = Order(2, dueInDays: -5, ordered: 1000, finished: 0, finishedLast7Days: 0);

        await Service().RunDelayCheckAsync(Today, default);

        waiting.DelayRiskSince.Should().BeNull();
        running.DelayRiskSince.Should().NotBeNull();   // đã quá hạn mà chưa hoàn thiện cái nào
    }

    [Fact]
    public async Task Delay_check_with_no_running_orders_does_nothing()
    {
        (await Service().RunDelayCheckAsync(Today, default)).Should().Be(0);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    // ---------- JOB-05 ----------

    [Fact]
    public async Task Purge_removes_only_notifications_older_than_the_retention_window()
    {
        var cutoff = Today.AddDays(-90).ToDateTime(TimeOnly.MinValue);
        _notifications.Add(new Notification { NotificationId = 1, CreatedAt = cutoff.AddDays(-1) });
        _notifications.Add(new Notification { NotificationId = 2, CreatedAt = cutoff.AddHours(1) });
        _notifications.Add(new Notification { NotificationId = 3, CreatedAt = Today.ToDateTime(TimeOnly.MinValue) });

        var removed = await Service().PurgeOldNotificationsAsync(Today, default);

        removed.Should().Be(1);
        _notifications.Select(n => n.NotificationId).Should().BeEquivalentTo(new long[] { 2, 3 });
    }

    [Fact]
    public async Task Retention_comes_from_the_system_setting()
    {
        _settings.GetIntAsync(SettingKeys.NotificationRetentionDays, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(10);
        _notifications.Add(new Notification { NotificationId = 1, CreatedAt = Today.AddDays(-11).ToDateTime(TimeOnly.MinValue) });
        _notifications.Add(new Notification { NotificationId = 2, CreatedAt = Today.AddDays(-9).ToDateTime(TimeOnly.MinValue) });

        (await Service().PurgeOldNotificationsAsync(Today, default)).Should().Be(1);
    }

    // ---------- Dashboard ----------

    [Fact]
    public async Task Overview_sums_the_order_lines_and_hides_closed_orders_by_default()
    {
        Order(1, 10, 1000, 0, 0);
        Order(2, 5, 500, 0, 0, status: OrderStatuses.Delivered);
        _view.Add(new VwOrderLineProgress { OrderLineId = 10, OrderId = 1, OrderedQty = 600, CutQty = 580, FinishedQty = 450, ReworkQty = 30, ScrapQty = 20, DeliveredQty = 100 });
        _view.Add(new VwOrderLineProgress { OrderLineId = 11, OrderId = 1, OrderedQty = 400, CutQty = 300, FinishedQty = 50, ReworkQty = 0, ScrapQty = 0 });

        var running = (await Service().GetOverviewAsync(false, default)).Data!;
        var all = (await Service().GetOverviewAsync(true, default)).Data!;

        running.Should().ContainSingle();
        var o = running.Single();
        o.OrderedQty.Should().Be(1000);
        o.CutQty.Should().Be(880);
        o.FinishedQty.Should().Be(500);
        o.DeliveredQty.Should().Be(100);
        o.DefectRatePct.Should().Be(Math.Round(100m * 50 / 550, 2));
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task Lines_of_an_unknown_order_are_not_found_and_known_orders_use_the_query_service()
    {
        Order(1, 10, 100, 0, 0);
        _output.GetOrderLineProgressAsync(1, Arg.Any<CancellationToken>()).Returns(Array.Empty<OrderLineProgressDto>());

        (await Service().GetLinesAsync(99, default)).ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        (await Service().GetLinesAsync(1, default)).IsSuccess.Should().BeTrue();
    }
}
