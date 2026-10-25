using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>TH-11 / UC44: QA/QC bàn giao thành phẩm, Thủ kho xác nhận vào kho FINISHED.</summary>
public class FgHandoverServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 15, 0, 0);
    private static readonly DateOnly Today = new(2026, 10, 27);
    private const int Inspector = 31;

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IOutputQueryService _output = Substitute.For<IOutputQueryService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<CustomerOrder> _orders = new();
    private readonly List<OrderLine> _orderLines = new();
    private readonly List<FgHandover> _handovers = new();
    private readonly List<FgHandoverLine> _handoverLines = new();
    private readonly Product _product = new() { ProductId = 3, ProductCode = "AO-01" };
    private readonly Employee _qa = new() { EmployeeId = Inspector, FullName = "QA Một" };
    private readonly CustomerOrder _order = new() { OrderId = 7, OrderCode = "DH-1", Status = OrderStatuses.InProduction };
    private readonly Dictionary<int, int> _qc = new();

    public FgHandoverServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _codes.NextAsync<FgHandover>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<FgHandover, string>>>(),
            Arg.Any<CancellationToken>()).Returns("TP-20261027-001");
        _output.GetQcFinishedByOrderLineAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(_ => (IReadOnlyDictionary<int, int>)new Dictionary<int, int>(_qc));
        _orders.Add(_order);
        Setup(_orders); Setup(_orderLines); Setup(_handovers); Setup(_handoverLines);
        AddLine(101, "Đen", "M", qc: 100);
        AddLine(102, "Đen", "L", qc: 60);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private FgHandoverService Service() =>
        new(_uow, _stock, _output, _codes, _notifier, _locks, _clock, NullLogger<FgHandoverService>.Instance);

    private OrderLine AddLine(int id, string color, string size, int qc)
    {
        var line = new OrderLine { OrderLineId = id, OrderId = 7, ProductId = 3, ProductVariant = TestVariant.Make(_product, color, size) };
        _orderLines.Add(line);
        _qc[id] = qc;
        return line;
    }

    private FgHandover Handover(string status, params (int OrderLineId, int Handed)[] lines)
    {
        var h = new FgHandover
        {
            FgHandoverId = _handovers.Count + 1, HandoverCode = $"TP-{_handovers.Count + 1}", OrderId = 7, Order = _order,
            ProductId = 3, Product = _product, InspectorId = Inspector, Inspector = _qa, HandedAt = Now, Status = status,
        };
        foreach (var (lineId, handed) in lines)
        {
            var line = new FgHandoverLine
            {
                FgHandoverLineId = h.FgHandoverId * 10 + h.FgHandoverLines.Count, FgHandoverId = h.FgHandoverId, FgHandover = h,
                OrderLineId = lineId, OrderLine = _orderLines.First(l => l.OrderLineId == lineId), HandedQty = handed,
            };
            h.FgHandoverLines.Add(line);
            _handoverLines.Add(line);
        }
        _handovers.Add(h);
        return h;
    }

    private static CreateFgHandoverDto Create(params (int OrderLineId, int Qty)[] lines) =>
        new(7, 3, lines.Select(l => new CreateHandoverLineDto(l.OrderLineId, l.Qty)).ToList());

    private List<FgHandover> CaptureAdds()
    {
        var added = new List<FgHandover>();
        _uow.Repository<FgHandover>().AddAsync(Arg.Do<FgHandover>(h =>
        {
            h.Order = _order; h.Product = _product; h.Inspector = _qa;
            foreach (var l in h.FgHandoverLines) { l.FgHandover = h; l.OrderLine = _orderLines.First(o => o.OrderLineId == l.OrderLineId); }
            added.Add(h); _handovers.Add(h); _handoverLines.AddRange(h.FgHandoverLines);
        }));
        return added;
    }

    // ---------- Số còn được bàn giao ----------

    [Fact]
    public async Task Handoverable_is_qc_passed_minus_pending_and_confirmed_handovers()
    {
        Handover(HandoverStatuses.Confirmed, (101, 30));
        Handover(HandoverStatuses.Pending, (101, 20));
        Handover(HandoverStatuses.Rejected, (101, 40));    // không tính
        Handover(HandoverStatuses.Cancelled, (102, 60));   // không tính

        var result = await Service().GetHandoverableAsync(7, default);

        var lines = result.Data!.ToDictionary(l => l.OrderLineId);
        lines[101].QcQty.Should().Be(100);
        lines[101].HandedQty.Should().Be(50);
        lines[101].RemainingQty.Should().Be(50);
        lines[102].RemainingQty.Should().Be(60);
    }

    [Fact]
    public async Task Handoverable_for_unknown_order_is_not_found()
    {
        var result = await Service().GetHandoverableAsync(999, default);

        result.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
    }

    // ---------- Lập phiếu ----------

    [Fact]
    public async Task Create_allows_a_partial_handover_and_notifies_the_warehouse_keeper()
    {
        var added = CaptureAdds();

        var result = await Service().CreateAsync(Create((101, 40), (102, 10)), Inspector, default);

        result.IsSuccess.Should().BeTrue();
        var handover = added.Should().ContainSingle().Subject;
        handover.Status.Should().Be(HandoverStatuses.Pending);
        handover.InspectorId.Should().Be(Inspector);
        handover.FgHandoverLines.Sum(l => l.HandedQty).Should().Be(50);
        result.Data!.Lines.Should().HaveCount(2);
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_cannot_exceed_qc_passed_qty()
    {
        var result = await Service().CreateAsync(Create((101, 101)), Inspector, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.FgHandoverExceedsQc);
        result.Message.Should().Contain("còn 100");
    }

    [Fact]
    public async Task Create_counts_a_pending_handover_against_the_remaining_qty()
    {
        Handover(HandoverStatuses.Pending, (101, 80));

        var result = await Service().CreateAsync(Create((101, 21)), Inspector, default);

        result.ErrorCode.Should().Be(ErrorCodes.FgHandoverExceedsQc);
        result.Message.Should().Contain("còn 20");
    }

    [Fact]
    public async Task Create_rejects_a_line_from_another_product_or_order()
    {
        _orderLines.Add(new OrderLine { OrderLineId = 201, OrderId = 7, ProductId = 4, ProductVariant = TestVariant.Make(new Product { ProductId = 4, ProductCode = "QU-02" }, "Be", "M") });

        var result = await Service().CreateAsync(Create((201, 5)), Inspector, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task Create_is_blocked_for_a_closed_order()
    {
        _order.Status = OrderStatuses.Delivered;

        var result = await Service().CreateAsync(Create((101, 5)), Inspector, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
    }

    // ---------- Xác nhận / từ chối / rút ----------

    [Fact]
    public async Task Confirm_posts_the_counted_qty_to_the_finished_warehouse()
    {
        var h = Handover(HandoverStatuses.Pending, (101, 40), (102, 10));
        var dto = new ConfirmFgHandoverDto(h.FgHandoverLines
            .Select(l => new ConfirmFgHandoverLineDto(l.FgHandoverLineId, l.HandedQty, null)).ToList());

        var result = await Service().ConfirmAsync(h.FgHandoverId, dto, 9, default);

        result.IsSuccess.Should().BeTrue();
        h.Status.Should().Be(HandoverStatuses.Confirmed);
        h.ConfirmedBy.Should().Be(9);
        await _stock.Received(1).PostAsync(
            Arg.Is<IReadOnlyList<StockMovement>>(m => m.Count == 2
                && m.All(x => x.Key.WarehouseType == WarehouseTypes.Finished && x.SourceDocType == StockDocTypes.FgHandover)
                && m.Sum(x => x.QtyChange) == 50),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_with_a_different_count_requires_a_reason()
    {
        var h = Handover(HandoverStatuses.Pending, (101, 40));
        var line = h.FgHandoverLines.Single();

        var noReason = await Service().ConfirmAsync(h.FgHandoverId,
            new ConfirmFgHandoverDto(new[] { new ConfirmFgHandoverLineDto(line.FgHandoverLineId, 38, null) }), 9, default);
        var withReason = await Service().ConfirmAsync(h.FgHandoverId,
            new ConfirmFgHandoverDto(new[] { new ConfirmFgHandoverLineDto(line.FgHandoverLineId, 38, " thiếu 2 áo ") }), 9, default);

        noReason.IsSuccess.Should().BeFalse();
        withReason.IsSuccess.Should().BeTrue();
        line.ConfirmedQty.Should().Be(38);
        line.DiscrepancyReason.Should().Be("thiếu 2 áo");
    }

    [Fact]
    public async Task Confirm_cannot_accept_more_than_handed_over()
    {
        var h = Handover(HandoverStatuses.Pending, (101, 40));

        var result = await Service().ConfirmAsync(h.FgHandoverId,
            new ConfirmFgHandoverDto(new[] { new ConfirmFgHandoverLineDto(h.FgHandoverLines.Single().FgHandoverLineId, 41, "x") }), 9, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        await _stock.DidNotReceive().PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_fails_when_the_stock_service_refuses()
    {
        var h = Handover(HandoverStatuses.Pending, (101, 40));
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.WarehouseInsufficientStock, "x"));

        var result = await Service().ConfirmAsync(h.FgHandoverId,
            new ConfirmFgHandoverDto(new[] { new ConfirmFgHandoverLineDto(h.FgHandoverLines.Single().FgHandoverLineId, 40, null) }), 9, default);

        result.IsSuccess.Should().BeFalse();
        h.Status.Should().Be(HandoverStatuses.Pending);
    }

    [Fact]
    public async Task A_decided_handover_cannot_be_confirmed_rejected_or_cancelled_again()
    {
        var h = Handover(HandoverStatuses.Confirmed, (101, 40));

        (await Service().RejectAsync(h.FgHandoverId, new ReasonDto("x"), 9, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        (await Service().CancelAsync(h.FgHandoverId, Inspector, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        (await Service().ConfirmAsync(h.FgHandoverId, new ConfirmFgHandoverDto(Array.Empty<ConfirmFgHandoverLineDto>()), 9, default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task Reject_stores_the_reason_and_releases_the_qty_for_a_new_handover()
    {
        var h = Handover(HandoverStatuses.Pending, (101, 100));

        var result = await Service().RejectAsync(h.FgHandoverId, new ReasonDto(" hàng chưa đủ "), 9, default);

        result.IsSuccess.Should().BeTrue();
        h.Status.Should().Be(HandoverStatuses.Rejected);
        h.RejectReason.Should().Be("hàng chưa đủ");
        var remaining = (await Service().GetHandoverableAsync(7, default)).Data!.Single(l => l.OrderLineId == 101).RemainingQty;
        remaining.Should().Be(100);
    }

    [Fact]
    public async Task Only_the_inspector_who_created_a_handover_can_cancel_it()
    {
        var h = Handover(HandoverStatuses.Pending, (101, 10));

        var other = await Service().CancelAsync(h.FgHandoverId, 999, default);
        var own = await Service().CancelAsync(h.FgHandoverId, Inspector, default);

        other.ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        own.IsSuccess.Should().BeTrue();
        h.Status.Should().Be(HandoverStatuses.Cancelled);
    }

    [Fact]
    public async Task An_inspector_cannot_open_another_inspectors_handover()
    {
        var h = Handover(HandoverStatuses.Pending, (101, 10));

        (await Service().GetByIdAsync(h.FgHandoverId, 999, default)).ErrorCode.Should().Be(ErrorCodes.CommonNotFound);
        (await Service().GetByIdAsync(h.FgHandoverId, Inspector, default)).IsSuccess.Should().BeTrue();
        (await Service().GetByIdAsync(h.FgHandoverId, null, default)).IsSuccess.Should().BeTrue();   // Thủ kho
    }

    // ---------- Validator ----------

    [Fact]
    public void Validators_reject_empty_duplicate_and_non_positive_lines()
    {
        var create = new CreateFgHandoverDtoValidator();
        create.Validate(Create((101, 5))).IsValid.Should().BeTrue();
        create.Validate(Create()).IsValid.Should().BeFalse();
        create.Validate(Create((101, 0))).IsValid.Should().BeFalse();
        create.Validate(Create((101, 5), (101, 6))).IsValid.Should().BeFalse();

        var confirm = new ConfirmFgHandoverDtoValidator();
        confirm.Validate(new ConfirmFgHandoverDto(new[] { new ConfirmFgHandoverLineDto(1, 0, null) })).IsValid.Should().BeTrue();
        confirm.Validate(new ConfirmFgHandoverDto(new[] { new ConfirmFgHandoverLineDto(1, -1, null) })).IsValid.Should().BeFalse();
        confirm.Validate(new ConfirmFgHandoverDto(Array.Empty<ConfirmFgHandoverLineDto>())).IsValid.Should().BeFalse();
    }
}
