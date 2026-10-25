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

/// <summary>TH-14 / UC45–UC47: phiếu xuất hàng — giữ chỗ, xuất kho, từ chối, khách nhận, trạng thái đơn hàng.</summary>
public class DispatchNoteServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 10, 0, 0);
    private static readonly DateOnly Today = new(2026, 10, 30);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IAppLock _locks = Substitute.For<IAppLock>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<CustomerOrder> _orders = new();
    private readonly List<OrderLine> _orderLines = new();
    private readonly List<DispatchNote> _notes = new();
    private readonly List<DispatchNoteLine> _noteLines = new();
    private readonly Product _product = new() { ProductId = 3, ProductCode = "AO-01" };
    private readonly CustomerOrder _order;

    public DispatchNoteServiceTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _stock.EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _stock.GetAvailableAsync(Arg.Any<StockKey>(), Arg.Any<CancellationToken>()).Returns(500m);
        _codes.NextAsync<DispatchNote>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<DispatchNote, string>>>(),
            Arg.Any<CancellationToken>()).Returns("XH-20261030-001");
        _order = new CustomerOrder
        {
            OrderId = 7, OrderCode = "DH-1", Status = OrderStatuses.InProduction,
            Customer = new Customer { CustomerName = "Khách A" },
        };
        _orders.Add(_order);
        Setup(_orders); Setup(_orderLines); Setup(_notes); Setup(_noteLines);
        AddLine(101, ordered: 100);
        AddLine(102, ordered: 50);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private DispatchNoteService Service() =>
        new(_uow, _stock, _codes, _notifier, _audit, _locks, _clock, NullLogger<DispatchNoteService>.Instance);

    private OrderLine AddLine(int id, int ordered)
    {
        var line = new OrderLine { OrderLineId = id, OrderId = 7, ProductId = 3, ProductVariant = TestVariant.Make(_product, "Đen", id == 101 ? "M" : "L"), OrderedQty = ordered };
        _orderLines.Add(line);
        return line;
    }

    private DispatchNote Note(string status, params (int OrderLineId, int Issue, int? Received)[] lines)
    {
        var note = new DispatchNote
        {
            DispatchNoteId = _notes.Count + 1, DispatchCode = $"XH-{_notes.Count + 1}", OrderId = 7, Order = _order,
            DeliveryDate = Today, DeliveryAddress = "Kho khách", Status = status, CreatedAt = Now,
        };
        foreach (var (lineId, issue, received) in lines)
        {
            var line = new DispatchNoteLine
            {
                DispatchNoteLineId = note.DispatchNoteId * 10 + note.DispatchNoteLines.Count, DispatchNoteId = note.DispatchNoteId,
                DispatchNote = note, OrderLineId = lineId, OrderLine = _orderLines.First(l => l.OrderLineId == lineId),
                IssueQty = issue, ReceivedQty = received,
            };
            note.DispatchNoteLines.Add(line);
            _noteLines.Add(line);
        }
        _notes.Add(note);
        return note;
    }

    private List<DispatchNote> CaptureAdds()
    {
        var added = new List<DispatchNote>();
        _uow.Repository<DispatchNote>().AddAsync(Arg.Do<DispatchNote>(n =>
        {
            n.Order = _order;
            foreach (var l in n.DispatchNoteLines) { l.DispatchNote = n; l.OrderLine = _orderLines.First(o => o.OrderLineId == l.OrderLineId); }
            added.Add(n); _notes.Add(n); _noteLines.AddRange(n.DispatchNoteLines);
        }));
        return added;
    }

    private static CreateDispatchNoteDto Create(params (int OrderLineId, int Qty)[] lines) =>
        new(7, Today.AddDays(2), "Kho khách, KCN A", null, lines.Select(l => new DispatchLineInputDto(l.OrderLineId, l.Qty)).ToList());

    // ---------- Lập phiếu (giữ chỗ) ----------

    [Fact]
    public async Task Create_reserves_finished_stock_and_notifies_the_warehouse_keeper()
    {
        var added = CaptureAdds();

        var result = await Service().CreateAsync(Create((101, 40), (102, 10)), default);

        result.IsSuccess.Should().BeTrue();
        var note = added.Should().ContainSingle().Subject;
        note.Status.Should().Be(DispatchStatuses.Pending);
        note.DispatchNoteLines.Sum(l => l.IssueQty).Should().Be(50);
        await _stock.Received(1).EnsureAvailableAsync(
            Arg.Is<IReadOnlyList<StockRequirement>>(r => r.Count == 2 && r.All(x => x.Key.WarehouseType == WarehouseTypes.Finished)),
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_is_refused_when_finished_stock_is_not_available()
    {
        _stock.EnsureAvailableAsync(Arg.Any<IReadOnlyList<StockRequirement>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.WarehouseInsufficientStock, "Tồn kho không đủ"));

        var result = await Service().CreateAsync(Create((101, 40)), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInsufficientStock);
        _notes.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_cannot_ship_more_than_what_is_left_of_the_order()
    {
        Note(DispatchStatuses.Delivered, (101, 60, 60));
        Note(DispatchStatuses.InDelivery, (101, 20, null));
        Note(DispatchStatuses.Pending, (101, 5, null));

        var result = await Service().CreateAsync(Create((101, 16)), default);   // đặt 100, đã xuất 85 → còn 15

        result.ErrorCode.Should().Be(ErrorCodes.DispatchExceedsOrder);
        result.Message.Should().Contain("còn 15");
    }

    [Fact]
    public async Task Rejected_and_cancelled_notes_do_not_use_up_the_order_qty()
    {
        Note(DispatchStatuses.Rejected, (101, 100, null));
        Note(DispatchStatuses.Cancelled, (101, 100, null));
        CaptureAdds();

        var result = await Service().CreateAsync(Create((101, 100)), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_rejects_a_line_that_is_not_part_of_the_order()
    {
        var result = await Service().CreateAsync(Create((999, 5)), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task Create_is_blocked_for_a_closed_order()
    {
        _order.Status = OrderStatuses.Cancelled;

        var result = await Service().CreateAsync(Create((101, 5)), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
    }

    [Fact]
    public async Task Dispatchable_lists_ordered_issued_remaining_and_available_stock()
    {
        Note(DispatchStatuses.InDelivery, (101, 30, null));

        var lines = (await Service().GetDispatchableAsync(7, default)).Data!.ToDictionary(l => l.OrderLineId);

        lines[101].OrderedQty.Should().Be(100);
        lines[101].IssuedQty.Should().Be(30);
        lines[101].RemainingQty.Should().Be(70);
        lines[101].AvailableQty.Should().Be(500m);
        lines[102].RemainingQty.Should().Be(50);
    }

    // ---------- Hủy / xuất / từ chối ----------

    [Fact]
    public async Task Cancel_only_works_for_a_pending_note()
    {
        var pending = Note(DispatchStatuses.Pending, (101, 10, null));
        var exported = Note(DispatchStatuses.InDelivery, (101, 10, null));

        (await Service().CancelAsync(pending.DispatchNoteId, default)).IsSuccess.Should().BeTrue();
        (await Service().CancelAsync(exported.DispatchNoteId, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);

        pending.Status.Should().Be(DispatchStatuses.Cancelled);
        exported.Status.Should().Be(DispatchStatuses.InDelivery);
    }

    [Fact]
    public async Task Export_deducts_finished_stock_and_moves_the_order_to_in_delivery()
    {
        var note = Note(DispatchStatuses.Pending, (101, 40, null), (102, 10, null));

        var result = await Service().ExportAsync(note.DispatchNoteId, 9, default);

        result.IsSuccess.Should().BeTrue();
        note.Status.Should().Be(DispatchStatuses.InDelivery);
        note.DispatchedBy.Should().Be(9);
        _order.Status.Should().Be(OrderStatuses.InDelivery);
        await _stock.Received(1).PostAsync(
            Arg.Is<IReadOnlyList<StockMovement>>(m => m.Count == 2 && m.All(x => x.QtyChange < 0
                && x.Key.WarehouseType == WarehouseTypes.Finished && x.SourceDocType == StockDocTypes.DispatchNote)
                && m.Sum(x => x.QtyChange) == -50),
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Accountant, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_keeps_the_note_pending_when_stock_cannot_be_posted()
    {
        var note = Note(DispatchStatuses.Pending, (101, 40, null));
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(ErrorCodes.WarehouseInsufficientStock, "x"));

        var result = await Service().ExportAsync(note.DispatchNoteId, 9, default);

        result.IsSuccess.Should().BeFalse();
        note.Status.Should().Be(DispatchStatuses.Pending);
        _order.Status.Should().Be(OrderStatuses.InProduction);
    }

    [Fact]
    public async Task Export_and_reject_only_work_for_pending_notes()
    {
        var note = Note(DispatchStatuses.InDelivery, (101, 10, null));

        (await Service().ExportAsync(note.DispatchNoteId, 9, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
        (await Service().RejectAsync(note.DispatchNoteId, new ReasonDto("x"), 9, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task Reject_stores_the_reason_and_releases_the_reservation()
    {
        var note = Note(DispatchStatuses.Pending, (101, 100, null));

        var result = await Service().RejectAsync(note.DispatchNoteId, new ReasonDto(" không đủ hàng "), 9, default);

        result.IsSuccess.Should().BeTrue();
        note.Status.Should().Be(DispatchStatuses.Rejected);
        note.RejectReason.Should().Be("không đủ hàng");
        (await Service().GetDispatchableAsync(7, default)).Data!.Single(l => l.OrderLineId == 101).RemainingQty.Should().Be(100);
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.Manager, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    // ---------- Khách nhận hàng ----------

    private static ConfirmDeliveryDto Delivery(DispatchNote note, DateOnly date, params int[] received) =>
        new(date, note.DispatchNoteLines.Select((l, i) => new DeliveryLineInputDto(l.DispatchNoteLineId, received[i])).ToList());

    private DispatchNote InDelivery(params (int OrderLineId, int Issue, int? Received)[] lines)
    {
        var note = Note(DispatchStatuses.InDelivery, lines);
        note.DispatchedAt = Now;
        return note;
    }

    [Fact]
    public async Task Confirm_delivery_with_everything_received_marks_note_and_order_delivered()
    {
        var note = InDelivery((101, 100, null), (102, 50, null));

        var result = await Service().ConfirmDeliveryAsync(note.DispatchNoteId, Delivery(note, Today.AddDays(1), 100, 50), default);

        result.IsSuccess.Should().BeTrue();
        note.Status.Should().Be(DispatchStatuses.Delivered);
        note.CustomerReceivedDate.Should().Be(Today.AddDays(1));
        _order.Status.Should().Be(OrderStatuses.Delivered);   // 150 nhận ≥ 150 đặt (GBR-23)
    }

    [Fact]
    public async Task Confirm_delivery_with_a_shortfall_is_partially_delivered()
    {
        var note = InDelivery((101, 100, null), (102, 50, null));

        await Service().ConfirmDeliveryAsync(note.DispatchNoteId, Delivery(note, Today, 100, 40), default);

        note.Status.Should().Be(DispatchStatuses.PartiallyDelivered);
        _order.Status.Should().Be(OrderStatuses.PartiallyDelivered);
    }

    [Fact]
    public async Task A_first_shipment_leaves_the_order_partially_delivered_and_the_last_one_completes_it()
    {
        var first = InDelivery((101, 60, null));
        await Service().ConfirmDeliveryAsync(first.DispatchNoteId, Delivery(first, Today, 60), default);
        _order.Status.Should().Be(OrderStatuses.PartiallyDelivered);

        var second = InDelivery((101, 40, null), (102, 50, null));
        await Service().ConfirmDeliveryAsync(second.DispatchNoteId, Delivery(second, Today, 40, 50), default);

        _order.Status.Should().Be(OrderStatuses.Delivered);
    }

    [Fact]
    public async Task Confirm_delivery_rejects_a_received_qty_above_the_issued_qty()
    {
        var note = InDelivery((101, 100, null));

        var result = await Service().ConfirmDeliveryAsync(note.DispatchNoteId, Delivery(note, Today, 101), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
        note.Status.Should().Be(DispatchStatuses.InDelivery);
    }

    [Fact]
    public async Task Confirm_delivery_rejects_a_date_before_the_goods_left_the_warehouse()
    {
        var note = InDelivery((101, 100, null));

        var result = await Service().ConfirmDeliveryAsync(note.DispatchNoteId, Delivery(note, Today.AddDays(-1), 100), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task Confirm_delivery_needs_every_line_and_an_in_delivery_note()
    {
        var note = InDelivery((101, 100, null), (102, 50, null));
        var missing = new ConfirmDeliveryDto(Today, new[] { new DeliveryLineInputDto(note.DispatchNoteLines.First().DispatchNoteLineId, 100) });
        (await Service().ConfirmDeliveryAsync(note.DispatchNoteId, missing, default)).ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);

        var pending = Note(DispatchStatuses.Pending, (101, 10, null));
        (await Service().ConfirmDeliveryAsync(pending.DispatchNoteId, Delivery(pending, Today, 10), default))
            .ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    // ---------- Quyền ảnh ----------

    [Theory]
    [InlineData(Roles.WarehouseKeeper, DispatchStatuses.Pending, true)]
    [InlineData(Roles.WarehouseKeeper, DispatchStatuses.InDelivery, true)]
    [InlineData(Roles.WarehouseKeeper, DispatchStatuses.Delivered, false)]
    [InlineData(Roles.Accountant, DispatchStatuses.Pending, false)]
    [InlineData(Roles.Accountant, DispatchStatuses.InDelivery, true)]
    [InlineData(Roles.Accountant, DispatchStatuses.Delivered, true)]
    [InlineData(Roles.Manager, DispatchStatuses.InDelivery, false)]
    public async Task Attachment_write_depends_on_role_and_status(string role, string status, bool allowed)
    {
        var user = Substitute.For<ICurrentUser>();
        user.Role.Returns(role);
        var note = Note(status, (101, 10, null));

        var access = new DispatchNoteAttachmentAccess(_uow, user);

        (await access.CanWriteAsync(note.DispatchNoteId, default)).Should().Be(allowed);
    }

    [Theory]
    [InlineData(Roles.WarehouseKeeper, true)]
    [InlineData(Roles.Manager, true)]
    [InlineData(Roles.Accountant, true)]
    [InlineData(Roles.Admin, true)]
    [InlineData(Roles.Worker, false)]
    public async Task Attachment_read_is_limited_to_the_dispatch_roles(string role, bool allowed)
    {
        var user = Substitute.For<ICurrentUser>();
        user.Role.Returns(role);
        var note = Note(DispatchStatuses.Pending, (101, 10, null));

        (await new DispatchNoteAttachmentAccess(_uow, user).CanReadAsync(note.DispatchNoteId, default)).Should().Be(allowed);
        (await new DispatchNoteAttachmentAccess(_uow, user).CanReadAsync(999, default)).Should().BeFalse();
    }

    // ---------- Validator ----------

    [Fact]
    public void Validators_require_address_date_lines_and_non_negative_received_qty()
    {
        var create = new CreateDispatchNoteDtoValidator();
        create.Validate(Create((101, 5))).IsValid.Should().BeTrue();
        create.Validate(Create()).IsValid.Should().BeFalse();
        create.Validate(Create((101, 0))).IsValid.Should().BeFalse();
        create.Validate(Create((101, 5), (101, 6))).IsValid.Should().BeFalse();
        create.Validate(Create((101, 5)) with { DeliveryAddress = " " }).IsValid.Should().BeFalse();

        var confirm = new ConfirmDeliveryDtoValidator();
        confirm.Validate(new ConfirmDeliveryDto(Today, new[] { new DeliveryLineInputDto(1, 0) })).IsValid.Should().BeTrue();
        confirm.Validate(new ConfirmDeliveryDto(Today, new[] { new DeliveryLineInputDto(1, -1) })).IsValid.Should().BeFalse();
        confirm.Validate(new ConfirmDeliveryDto(Today, Array.Empty<DeliveryLineInputDto>())).IsValid.Should().BeFalse();
    }
}
