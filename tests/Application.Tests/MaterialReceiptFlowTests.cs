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

/// <summary>
/// TH-12 / TH-13: nhập NVL từ nhà cung cấp theo đơn mua; thiếu hàng tự tính khi đã nhận đủ số lần giao dự kiến
/// (<c>ExpectedDeliveryCount</c>) mà còn thiếu → đơn SHORT_DELIVERY; phiếu bổ sung = phiếu có ShortageRequestId.
/// </summary>
public class MaterialReceiptFlowTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 9, 0, 0);
    private static readonly DateOnly Today = new(2026, 10, 27);

    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IStockService _stock = Substitute.For<IStockService>();
    private readonly IDocumentCodeGenerator _codes = Substitute.For<IDocumentCodeGenerator>();
    private readonly IRequestService _requests = Substitute.For<IRequestService>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly IAuditLogService _audit = Substitute.For<IAuditLogService>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private readonly List<MaterialReceipt> _receipts = new();
    private readonly List<MaterialReceiptLine> _receiptLines = new();
    private readonly List<PurchaseOrder> _pos = new();
    private readonly List<PurchaseOrderLine> _poLines = new();
    private readonly List<CustomerOrder> _orders = new();
    private readonly List<OrderMaterial> _orderMaterials = new();
    private readonly List<Request> _requestList = new();
    private readonly List<RequestLine> _requestLines = new();
    private readonly List<Attachment> _attachments = new();

    public MaterialReceiptFlowTests()
    {
        _clock.Now.Returns(Now);
        _clock.Today.Returns(Today);
        _stock.PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _requests.MarkCompletedAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        _requests.StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>())
            .Returns(_ => new Request { RequestId = 900, RequestCode = "YC261027-001" });
        Setup(_receipts); Setup(_receiptLines); Setup(_pos); Setup(_poLines); Setup(_orders); Setup(_orderMaterials);
        Setup(_requestList); Setup(_requestLines); Setup(_attachments);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private MaterialSupplyUpdater Supply() => new(_uow);

    private MaterialReceiptService Receipts() =>
        new(_uow, _stock, _codes, _requests, Supply(), _clock, NullLogger<MaterialReceiptService>.Instance);

    private ShortDeliveryService ShortDeliveries() => new(_uow, _requests, Supply(), _notifier, _audit);

    // ---------- Dữ liệu mẫu ----------

    private static readonly Material Fabric = new() { MaterialId = 1, MaterialCode = "V-0001", MaterialName = "Vải", Unit = "m" };

    private (PurchaseOrder Po, PurchaseOrderLine Line, OrderMaterial Om, CustomerOrder Order) PlacedPo(
        string goodsStatus = PurchaseGoodsStatuses.Ordered, decimal ordered = 100, decimal required = 100,
        short? expectedDeliveries = null)
    {
        var order = new CustomerOrder { OrderId = 7, OrderCode = "DH-1", Status = OrderStatuses.InProduction, MaterialStatus = OrderMaterialStatuses.Awaiting };
        var om = new OrderMaterial { OrderMaterialId = 21, OrderId = 7, MaterialId = 1, Material = Fabric, MaterialColor = "Đen", RequiredQty = required };
        var po = new PurchaseOrder { PurchaseOrderId = 5, PoCode = "DM-001", OrderId = 7, GoodsStatus = goodsStatus, ExpectedDeliveryCount = expectedDeliveries };
        var line = new PurchaseOrderLine { PurchaseOrderLineId = 11, PurchaseOrderId = 5, PurchaseOrder = po, OrderMaterialId = 21, OrderMaterial = om, OrderedQty = ordered };
        po.PurchaseOrderLines.Add(line);
        _orders.Add(order); _orderMaterials.Add(om); _pos.Add(po); _poLines.Add(line);
        return (po, line, om, order);
    }

    private MaterialReceipt PendingSupplierReceipt(PurchaseOrder po, PurchaseOrderLine pol, OrderMaterial om,
        decimal accepted, int id = 50, int? shortageRequestId = null)
    {
        var receipt = new MaterialReceipt
        {
            MaterialReceiptId = id, ReceiptCode = $"PN-{id}", ShortageRequestId = shortageRequestId, Source = MaterialSources.Supplier,
            OrderId = po.OrderId, PurchaseOrderId = po.PurchaseOrderId, Status = ReceiptStatuses.Pending,
        };
        var line = new MaterialReceiptLine
        {
            MaterialReceiptLineId = id * 10 + 1, MaterialReceiptId = id, MaterialReceipt = receipt, OrderMaterialId = om.OrderMaterialId,
            OrderMaterial = om, PurchaseOrderLineId = pol.PurchaseOrderLineId, PurchaseOrderLine = pol,
            AcceptedQty = accepted,
        };
        receipt.MaterialReceiptLines.Add(line);
        _receipts.Add(receipt); _receiptLines.Add(line);
        AddPhoto(id);   // mặc định phiếu đã có ảnh chứng từ; test "thiếu ảnh" gỡ đi
        return receipt;
    }

    private void AddPhoto(int receiptId) => _attachments.Add(new Attachment
    {
        OwnerType = AttachmentOwnerTypes.MaterialReceipt, OwnerId = receiptId, Purpose = AttachmentPurposes.DocumentPhoto, IsCurrent = true,
    });

    private static SaveMaterialReceiptDto SupplierDto(int poId, int polId, decimal accepted) =>
        new(null, MaterialSources.Supplier, poId, Today, null,
            new[] { new SaveReceiptLineDto(null, polId, accepted) });

    // ---------- Lập phiếu nhập từ NCC (TH-12) ----------

    [Fact]
    public async Task Create_supplier_receipt_is_rejected_when_purchase_order_is_not_placed()
    {
        var (po, line, _, _) = PlacedPo(PurchaseOrderStatuses.PendingFinalisation);

        var result = await Receipts().CreateAsync(SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 10), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    [Fact]
    public async Task Create_supplier_receipt_is_rejected_when_purchase_line_belongs_to_another_order()
    {
        var (po, _, _, _) = PlacedPo();

        var result = await Receipts().CreateAsync(SupplierDto(po.PurchaseOrderId, 999, 10), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidReference);
    }

    [Fact]
    public async Task Create_supplier_receipt_cannot_exceed_outstanding_qty()
    {
        var (po, line, om, _) = PlacedPo(ordered: 100);
        line.NotReplenishedQty = 10;
        var earlier = PendingSupplierReceipt(po, line, om, accepted: 60, id: 40);
        earlier.Status = ReceiptStatuses.Confirmed;   // đã nhận 60 + không bù 10 → còn 30

        var over = await Receipts().CreateAsync(SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 31), default);

        over.IsSuccess.Should().BeFalse();
        over.ErrorCode.Should().Be(ErrorCodes.ReceiptExceedsPurchase);
        over.Message.Should().Contain("còn 30");
    }

    /// <summary>Gắn navigation cho phiếu mới thêm để GetByIdAsync (LINQ to Objects) chạy được như EF.</summary>
    private List<MaterialReceipt> CaptureAdds(OrderMaterial om, PurchaseOrderLine pol)
    {
        var added = new List<MaterialReceipt>();
        _uow.Repository<MaterialReceipt>().AddAsync(Arg.Do<MaterialReceipt>(r =>
        {
            foreach (var l in r.MaterialReceiptLines) { l.MaterialReceipt = r; l.OrderMaterial = om; l.PurchaseOrderLine = pol; }
            added.Add(r); _receipts.Add(r); _receiptLines.AddRange(r.MaterialReceiptLines);
        }));
        return added;
    }

    [Fact]
    public async Task Create_supplier_receipt_takes_order_and_target_from_the_purchase_order()
    {
        var (po, line, om, _) = PlacedPo(ordered: 100);
        var added = CaptureAdds(om, line);
        _codes.NextAsync<MaterialReceipt>(Arg.Any<string>(), Arg.Any<System.Linq.Expressions.Expression<Func<MaterialReceipt, string>>>(), Arg.Any<CancellationToken>())
            .Returns("PN-NEW");

        var result = await Receipts().CreateAsync(SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 100), default);

        result.IsSuccess.Should().BeTrue();
        var receipt = added.Should().ContainSingle().Subject;
        receipt.PurchaseOrderId.Should().Be(5);   // Source là cột tính của DB (có đơn mua = NCC)
        receipt.OrderId.Should().Be(7);
        receipt.Status.Should().Be(ReceiptStatuses.Pending);
        var saved = receipt.MaterialReceiptLines.Should().ContainSingle().Subject;
        saved.PurchaseOrderLineId.Should().Be(11);
        saved.OrderMaterialId.Should().Be(21);
        result.Data!.Lines.Single().MaterialCode.Should().Be("V-0001");
    }

    [Fact]
    public async Task A_second_receipt_for_the_same_purchase_line_is_blocked_while_one_is_pending()
    {
        var (po, line, om, _) = PlacedPo(ordered: 100);
        PendingSupplierReceipt(po, line, om, accepted: 90, id: 41);   // còn chờ Quản đốc: không gửi tiếp cùng NVL
        CaptureAdds(om, line);

        var result = await Receipts().CreateAsync(SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 10), default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptDuplicatePending);
        result.Message.Should().Contain("PN-41");
    }

    [Fact]
    public async Task Editing_the_pending_receipt_itself_is_not_a_duplicate_and_a_returned_one_does_not_block()
    {
        var (po, line, om, _) = PlacedPo(ordered: 100);
        var pending = PendingSupplierReceipt(po, line, om, accepted: 90, id: 41);

        _uow.When(u => u.SaveChangesAsync()).Do(_ =>   // gắn navigation cho dòng mới như EF
        {
            foreach (var l in pending.MaterialReceiptLines) { l.MaterialReceipt = pending; l.OrderMaterial = om; l.PurchaseOrderLine = line; }
        });
        var edited = await Receipts().UpdateAsync(41, SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 50), default);
        edited.IsSuccess.Should().BeTrue();

        pending.Status = ReceiptStatuses.Returned;
        CaptureAdds(om, line);
        var next = await Receipts().CreateAsync(SupplierDto(po.PurchaseOrderId, line.PurchaseOrderLineId, 10), default);
        next.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Confirming_checks_the_cap_again_so_two_receipts_cannot_both_add_the_full_quantity()
    {
        var (po, line, om, _) = PlacedPo(ordered: 100);
        var first = PendingSupplierReceipt(po, line, om, accepted: 100, id: 41);
        var second = PendingSupplierReceipt(po, line, om, accepted: 100, id: 42);   // lập khi phiếu 1 chưa được xác nhận (dữ liệu cũ)

        (await Receipts().ConfirmAsync(first.MaterialReceiptId, 3, default)).IsSuccess.Should().BeTrue();
        var result = await Receipts().ConfirmAsync(second.MaterialReceiptId, 3, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptExceedsPurchase);
        result.Message.Should().Contain("còn 0");
        second.Status.Should().Be(ReceiptStatuses.Pending);
        await _stock.Received(1).PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>());   // chỉ phiếu 1 vào kho
    }

    private static SaveMaterialReceiptDto CustomerDto(int orderMaterialId, decimal accepted) =>
        new(7, MaterialSources.Customer, null, Today, null, new[] { new SaveReceiptLineDto(orderMaterialId, null, accepted) });

    [Fact]
    public async Task Customer_receipt_cannot_exceed_the_customer_supplied_qty_and_cannot_be_sent_twice()
    {
        var (_, _, om, _) = PlacedPo(ordered: 100);
        om.CustomerQty = 50;
        _receipts.Add(new MaterialReceipt { MaterialReceiptId = 30, ReceiptCode = "PN-30", OrderId = 7, Status = ReceiptStatuses.Confirmed });
        _receiptLines.Add(new MaterialReceiptLine { MaterialReceiptLineId = 300, MaterialReceiptId = 30, OrderMaterialId = 21, AcceptedQty = 30,
            MaterialReceipt = _receipts.Last() });
        CaptureAdds(om, new PurchaseOrderLine());

        var tooMuch = await Receipts().CreateAsync(CustomerDto(21, 25), default);   // còn 20
        tooMuch.ErrorCode.Should().Be(ErrorCodes.ReceiptExceedsPurchase);
        tooMuch.Message.Should().Contain("còn 20");

        var ok = await Receipts().CreateAsync(CustomerDto(21, 20), default);
        ok.IsSuccess.Should().BeTrue();

        var again = await Receipts().CreateAsync(CustomerDto(21, 20), default);   // phiếu trước còn chờ
        again.ErrorCode.Should().Be(ErrorCodes.ReceiptDuplicatePending);
    }

    // ---------- Xác nhận phiếu: trạng thái đơn mua, MaterialStatus, đơn thiếu (TH-12 / TH-13) ----------

    [Fact]
    public async Task Confirm_full_delivery_marks_purchase_order_received_and_materials_complete()
    {
        var (po, line, om, order) = PlacedPo(ordered: 100, required: 100);
        var receipt = PendingSupplierReceipt(po, line, om, accepted: 100);

        var result = await Receipts().ConfirmAsync(receipt.MaterialReceiptId, 3, default);

        result.IsSuccess.Should().BeTrue();
        po.GoodsStatus.Should().Be(PurchaseGoodsStatuses.Received);
        order.MaterialStatus.Should().Be(OrderMaterialStatuses.Complete);
        await _requests.DidNotReceive().StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_partial_delivery_with_shortfall_stages_short_delivery_request()
    {
        // Mặc định NCC giao 1 lần: phiếu xác nhận đầu tiên là lần giao cuối, còn thiếu 20 → tự sinh đơn thiếu NVL.
        var (po, line, om, order) = PlacedPo(ordered: 100, required: 100);
        var receipt = PendingSupplierReceipt(po, line, om, accepted: 80);
        var staged = new Request { RequestId = 900, RequestCode = "YC261027-001" };
        _requests.StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>()).Returns(staged);

        var result = await Receipts().ConfirmAsync(receipt.MaterialReceiptId, 3, default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain(WarehouseMessages.ShortDeliveryCreated);   // MSG31
        po.GoodsStatus.Should().Be(PurchaseGoodsStatuses.PartiallyReceived);
        order.MaterialStatus.Should().Be(OrderMaterialStatuses.Partial);
        await _requests.Received(1).StageSystemRequestAsync(
            Arg.Is<RequestDraft>(d => d.RequestType == RequestTypes.ShortDelivery && d.MaterialReceiptId == 50 && d.OrderId == 7),
            Arg.Any<CancellationToken>());
        var requestLine = staged.RequestLines.Should().ContainSingle().Subject;
        requestLine.Quantity.Should().Be(20);
        requestLine.MaterialId.Should().Be(1);
        requestLine.MaterialColor.Should().Be("Đen");
        requestLine.MaterialReceiptLineId.Should().Be(501);
    }

    [Fact]
    public async Task Confirm_does_not_stage_a_shortage_while_more_deliveries_are_expected()
    {
        var (po, line, om, order) = PlacedPo(ordered: 100, required: 100, expectedDeliveries: 2);
        var receipt = PendingSupplierReceipt(po, line, om, accepted: 60);

        var result = await Receipts().ConfirmAsync(receipt.MaterialReceiptId, 3, default);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().NotContain(WarehouseMessages.ShortDeliveryCreated);
        po.GoodsStatus.Should().Be(PurchaseGoodsStatuses.PartiallyReceived);
        await _requests.DidNotReceive().StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_stages_the_shortage_once_the_expected_number_of_deliveries_is_reached()
    {
        var (po, line, om, _) = PlacedPo(ordered: 100, required: 100, expectedDeliveries: 2);
        var first = PendingSupplierReceipt(po, line, om, accepted: 60, id: 40);
        first.Status = ReceiptStatuses.Confirmed;
        var second = PendingSupplierReceipt(po, line, om, accepted: 30, id: 50);
        var staged = new Request { RequestId = 900, RequestCode = "YC261027-001" };
        _requests.StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>()).Returns(staged);

        var result = await Receipts().ConfirmAsync(second.MaterialReceiptId, 3, default);

        result.Message.Should().Contain(WarehouseMessages.ShortDeliveryCreated);
        staged.RequestLines.Should().ContainSingle().Which.Quantity.Should().Be(10);   // 100 − 60 − 30
    }

    [Fact]
    public async Task Confirm_does_not_stage_a_second_shortage_while_one_is_still_open()
    {
        var (po, line, om, _) = PlacedPo(ordered: 100, required: 100);
        var first = PendingSupplierReceipt(po, line, om, accepted: 80, id: 40);
        first.Status = ReceiptStatuses.Confirmed;
        OpenShortDelivery(first, qty: 20);
        var again = PendingSupplierReceipt(po, line, om, accepted: 5, id: 50);

        await Receipts().ConfirmAsync(again.MaterialReceiptId, 3, default);

        await _requests.DidNotReceive().StageSystemRequestAsync(Arg.Any<RequestDraft>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_posts_only_the_accepted_qty_to_the_material_warehouse()
    {
        var (po, line, om, _) = PlacedPo();
        var receipt = PendingSupplierReceipt(po, line, om, accepted: 80);

        await Receipts().ConfirmAsync(receipt.MaterialReceiptId, 3, default);

        await _stock.Received(1).PostAsync(
            Arg.Is<IReadOnlyList<StockMovement>>(m => m.Count == 1 && m[0].QtyChange == 80
                && m[0].Key.MaterialId == 1 && m[0].Key.MaterialColor == "Đen"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_requires_the_delivery_document_photo()
    {
        var (po, line, om, _) = PlacedPo();
        var receipt = PendingSupplierReceipt(po, line, om, accepted: 10);
        _attachments.Clear();

        var result = await Receipts().ConfirmAsync(receipt.MaterialReceiptId, 3, default);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ReceiptPhotoRequired);
        receipt.Status.Should().Be(ReceiptStatuses.Pending);
        await _stock.DidNotReceive().PostAsync(Arg.Any<IReadOnlyList<StockMovement>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_ignores_replaced_photos_when_looking_for_the_current_one()
    {
        var (po, line, om, _) = PlacedPo();
        var receipt = PendingSupplierReceipt(po, line, om, accepted: 10);
        _attachments.Single().IsCurrent = false;   // ảnh cũ đã bị thay

        (await Receipts().ConfirmAsync(receipt.MaterialReceiptId, 3, default)).ErrorCode.Should().Be(ErrorCodes.ReceiptPhotoRequired);
    }

    [Fact]
    public async Task Confirm_fails_for_a_receipt_that_is_not_pending()
    {
        var (po, line, om, _) = PlacedPo();
        var receipt = PendingSupplierReceipt(po, line, om, accepted: 10);
        receipt.Status = ReceiptStatuses.Confirmed;

        var result = await Receipts().ConfirmAsync(receipt.MaterialReceiptId, 3, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseInvalidStatus);
    }

    // ---------- Xử lý đơn thiếu NVL (TH-13) ----------

    private (Request Request, RequestLine Line) OpenShortDelivery(MaterialReceipt receipt, decimal qty = 20)
    {
        var request = new Request
        {
            RequestId = 900, RequestCode = "YC261027-001", RequestTypeCode = RequestTypes.ShortDelivery,
            Status = RequestStatuses.PendingProcessing, OrderId = receipt.OrderId, MaterialReceiptId = receipt.MaterialReceiptId,
            MaterialReceipt = receipt,
        };
        var line = new RequestLine
        {
            RequestLineId = 901, RequestId = 900, Request = request, MaterialId = 1, Material = Fabric, MaterialColor = "Đen",
            Quantity = qty, MaterialReceiptLineId = receipt.MaterialReceiptLines.First().MaterialReceiptLineId,
            MaterialReceiptLine = receipt.MaterialReceiptLines.First(),
        };
        request.RequestLines.Add(line);
        _requestList.Add(request); _requestLines.Add(line);
        return (request, line);
    }

    [Fact]
    public async Task Resolve_no_replenish_closes_the_shortfall_and_marks_purchase_order_closed_short()
    {
        var (po, pol, om, order) = PlacedPo(PurchaseGoodsStatuses.PartiallyReceived, ordered: 100, required: 100);
        var receipt = PendingSupplierReceipt(po, pol, om, accepted: 80);
        receipt.Status = ReceiptStatuses.Confirmed;
        var (request, _) = OpenShortDelivery(receipt);

        var result = await ShortDeliveries().ResolveAsync(900,
            new ResolveShortDeliveryDto(ShortageResolutions.NoReplenish, null, "Nhà cung cấp hết hàng"), 3, default);

        result.IsSuccess.Should().BeTrue();
        request.ShortageResolution.Should().Be(ShortageResolutions.NoReplenish);
        pol.NotReplenishedQty.Should().Be(20);
        po.GoodsStatus.Should().Be(PurchaseGoodsStatuses.ClosedShort);
        order.MaterialStatus.Should().Be(OrderMaterialStatuses.Complete);   // đã nhận 80 + không bù 20 = đủ 100
        await _requests.Received(1).MarkCompletedAsync(900, "Nhà cung cấp hết hàng", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_no_replenish_never_pushes_received_plus_not_replenished_above_ordered()
    {
        var (po, pol, om, _) = PlacedPo(PurchaseGoodsStatuses.PartiallyReceived, ordered: 100);
        var receipt = PendingSupplierReceipt(po, pol, om, accepted: 95);
        receipt.Status = ReceiptStatuses.Confirmed;
        OpenShortDelivery(receipt, qty: 50);   // số thiếu khai lớn hơn phần còn lại trên đơn mua

        await ShortDeliveries().ResolveAsync(900, new ResolveShortDeliveryDto(ShortageResolutions.NoReplenish, null, null), 3, default);

        pol.NotReplenishedQty.Should().Be(5);   // chỉ còn 100 − 95 = 5 để chốt
    }

    [Fact]
    public async Task Resolve_replenish_records_qty_per_line_and_notifies_warehouse_keeper()
    {
        var (po, pol, om, _) = PlacedPo(PurchaseGoodsStatuses.PartiallyReceived);
        var receipt = PendingSupplierReceipt(po, pol, om, accepted: 80);
        receipt.Status = ReceiptStatuses.Confirmed;
        var (request, line) = OpenShortDelivery(receipt);

        var result = await ShortDeliveries().ResolveAsync(900, new ResolveShortDeliveryDto(ShortageResolutions.Replenish,
            new[] { new ReplenishInputDto(901, 15) }, null), 3, default);

        result.IsSuccess.Should().BeTrue();
        request.ShortageResolution.Should().Be(ShortageResolutions.Replenish);
        line.ReplenishQty.Should().Be(15);
        request.Status.Should().Be(RequestStatuses.PendingProcessing);   // chưa xong: chờ phiếu bổ sung
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await _requests.DidNotReceive().MarkCompletedAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_replenish_defaults_to_the_whole_shortfall()
    {
        var (po, pol, om, _) = PlacedPo(PurchaseGoodsStatuses.PartiallyReceived);
        var receipt = PendingSupplierReceipt(po, pol, om, accepted: 80);
        receipt.Status = ReceiptStatuses.Confirmed;
        var (_, line) = OpenShortDelivery(receipt, qty: 20);

        await ShortDeliveries().ResolveAsync(900, new ResolveShortDeliveryDto(ShortageResolutions.Replenish, null, null), 3, default);

        line.ReplenishQty.Should().Be(20);
    }

    [Fact]
    public async Task Resolve_replenish_rejects_qty_above_the_shortfall()
    {
        var (po, pol, om, _) = PlacedPo(PurchaseGoodsStatuses.PartiallyReceived);
        var receipt = PendingSupplierReceipt(po, pol, om, accepted: 80);
        receipt.Status = ReceiptStatuses.Confirmed;
        OpenShortDelivery(receipt, qty: 20);

        var result = await ShortDeliveries().ResolveAsync(900, new ResolveShortDeliveryDto(ShortageResolutions.Replenish,
            new[] { new ReplenishInputDto(901, 21) }, null), 3, default);

        result.ErrorCode.Should().Be(ErrorCodes.ShortageExceeded);
    }

    [Fact]
    public async Task A_resolved_short_delivery_cannot_be_resolved_twice()
    {
        var (po, pol, om, _) = PlacedPo(PurchaseGoodsStatuses.PartiallyReceived);
        var receipt = PendingSupplierReceipt(po, pol, om, accepted: 80);
        receipt.Status = ReceiptStatuses.Confirmed;
        var (request, _) = OpenShortDelivery(receipt);
        request.ShortageResolution = ShortageResolutions.Replenish;

        var result = await ShortDeliveries().ResolveAsync(900, new ResolveShortDeliveryDto(ShortageResolutions.NoReplenish, null, null), 3, default);

        result.ErrorCode.Should().Be(ErrorCodes.ShortageNotResolvable);
    }

    // ---------- Phiếu bổ sung ----------

    [Fact]
    public async Task Supplementary_receipt_requires_the_replenish_resolution()
    {
        var (po, pol, om, _) = PlacedPo(PurchaseGoodsStatuses.PartiallyReceived);
        var original = PendingSupplierReceipt(po, pol, om, accepted: 80);
        original.Status = ReceiptStatuses.Confirmed;
        OpenShortDelivery(original);   // chưa chọn giao bù

        var result = await Receipts().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(900, Today, null,
            new[] { new SupplementaryLineDto(501, 10) }), default);

        result.ErrorCode.Should().Be(ErrorCodes.ShortageNotResolvable);
    }

    [Fact]
    public async Task Supplementary_receipt_cannot_receive_more_than_the_remaining_replenish_qty()
    {
        var (po, pol, om, _) = PlacedPo(PurchaseGoodsStatuses.PartiallyReceived);
        var original = PendingSupplierReceipt(po, pol, om, accepted: 80);
        original.Status = ReceiptStatuses.Confirmed;
        var (request, line) = OpenShortDelivery(original);
        request.ShortageResolution = ShortageResolutions.Replenish;
        line.ReplenishQty = 20;
        PendingSupplierReceipt(po, pol, om, accepted: 12, id: 60, shortageRequestId: 900);
        _receiptLines.Last().OriginalReceiptLineId = 501;   // đã có phiếu bổ sung chờ xác nhận 12

        var result = await Receipts().CreateSupplementaryAsync(new SaveSupplementaryReceiptDto(900, Today, null,
            new[] { new SupplementaryLineDto(501, 9) }), default);   // chỉ còn 8

        result.ErrorCode.Should().Be(ErrorCodes.ShortageExceeded);
        result.Message.Should().Contain("còn 8");
    }

    [Fact]
    public async Task Confirming_supplementary_receipt_completes_the_request_only_when_fully_replenished()
    {
        var (po, pol, om, _) = PlacedPo(PurchaseGoodsStatuses.PartiallyReceived);
        var original = PendingSupplierReceipt(po, pol, om, accepted: 80);
        original.Status = ReceiptStatuses.Confirmed;
        var (request, line) = OpenShortDelivery(original);
        request.ShortageResolution = ShortageResolutions.Replenish;
        line.ReplenishQty = 20;

        PendingSupplierReceipt(po, pol, om, accepted: 12, id: 60, shortageRequestId: 900);
        _receiptLines.Last().OriginalReceiptLineId = 501;
        await Receipts().ConfirmAsync(60, 3, default);
        await _requests.DidNotReceive().MarkCompletedAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        PendingSupplierReceipt(po, pol, om, accepted: 8, id: 61, shortageRequestId: 900);
        _receiptLines.Last().OriginalReceiptLineId = 501;
        await Receipts().ConfirmAsync(61, 3, default);

        await _requests.Received(1).MarkCompletedAsync(900, Arg.Any<string?>(), Arg.Any<CancellationToken>());
        po.GoodsStatus.Should().Be(PurchaseGoodsStatuses.Received);   // 80 + 12 + 8 = 100
    }

    // ---------- Validator ----------

    [Fact]
    public void Validator_requires_order_for_customer_and_purchase_order_for_supplier()
    {
        var validator = new SaveMaterialReceiptDtoValidator();
        var line = new[] { new SaveReceiptLineDto(1, null, 5) };
        var supplierLine = new[] { new SaveReceiptLineDto(null, 3, 5) };

        validator.Validate(new SaveMaterialReceiptDto(null, MaterialSources.Customer, null, Today, null, line)).IsValid.Should().BeFalse();
        validator.Validate(new SaveMaterialReceiptDto(7, MaterialSources.Customer, null, Today, null, line)).IsValid.Should().BeTrue();
        validator.Validate(new SaveMaterialReceiptDto(null, MaterialSources.Supplier, null, Today, null, supplierLine)).IsValid.Should().BeFalse();
        validator.Validate(new SaveMaterialReceiptDto(null, MaterialSources.Supplier, 5, Today, null, supplierLine)).IsValid.Should().BeTrue();
        validator.Validate(new SaveMaterialReceiptDto(7, "OTHER", null, Today, null, line)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_rejects_a_line_with_neither_order_material_nor_purchase_line()
    {
        var validator = new SaveMaterialReceiptDtoValidator();
        var dto = new SaveMaterialReceiptDto(7, MaterialSources.Customer, null, Today, null,
            new[] { new SaveReceiptLineDto(null, null, 5) });

        validator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Resolve_validator_accepts_only_the_two_resolutions()
    {
        var validator = new ResolveShortDeliveryDtoValidator();

        validator.Validate(new ResolveShortDeliveryDto(ShortageResolutions.Replenish, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new ResolveShortDeliveryDto(ShortageResolutions.NoReplenish, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new ResolveShortDeliveryDto("MAYBE", null, null)).IsValid.Should().BeFalse();
    }

    // ---------- Tính lại trạng thái ----------

    [Fact]
    public async Task Material_status_is_awaiting_until_something_is_received_then_partial()
    {
        var (po, pol, om, order) = PlacedPo(ordered: 100, required: 100);
        await Supply().RecalculateMaterialStatusAsync(7, default);
        order.MaterialStatus.Should().Be(OrderMaterialStatuses.Awaiting);

        var receipt = PendingSupplierReceipt(po, pol, om, accepted: 40);
        receipt.Status = ReceiptStatuses.Confirmed;
        await Supply().RecalculateMaterialStatusAsync(7, default);

        order.MaterialStatus.Should().Be(OrderMaterialStatuses.Partial);
    }

    [Fact]
    public async Task Pending_or_returned_receipts_do_not_count_as_received()
    {
        var (po, pol, om, order) = PlacedPo(ordered: 100, required: 100);
        PendingSupplierReceipt(po, pol, om, accepted: 100, id: 70);   // PENDING
        PendingSupplierReceipt(po, pol, om, accepted: 100, id: 71).Status = ReceiptStatuses.Returned;

        await Supply().RecalculateMaterialStatusAsync(7, default);
        await Supply().RecalculatePurchaseOrderAsync(5, default);

        order.MaterialStatus.Should().Be(OrderMaterialStatuses.Awaiting);
        po.GoodsStatus.Should().Be(PurchaseGoodsStatuses.Ordered);
    }
}
