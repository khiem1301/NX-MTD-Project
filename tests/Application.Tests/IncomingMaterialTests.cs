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
/// Đơn chờ khai báo NVL (Quản đốc / Admin) và màn NVL chờ nhận (Thủ kho): NVL khách sẽ cấp và đơn mua đã đặt mà kho chưa nhận đủ,
/// kèm số lần giao dự kiến và số lần đã nhận (vw_OrderMaterialDelivery / vw_PurchaseOrderDelivery).
/// </summary>
public class IncomingMaterialTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<OrderMaterial> _orderMaterials = [];
    private readonly List<PurchaseOrderLine> _poLines = [];
    private readonly List<MaterialReceiptLine> _receiptLines = [];
    private readonly List<Material> _materials = [];
    private readonly List<OrderLine> _orderLines = [];
    private readonly List<VwOrderMaterialDelivery> _omDeliveries = [];
    private readonly List<VwPurchaseOrderDelivery> _poDeliveries = [];
    private readonly List<ProductComponentMaterial> _norms = [];
    private readonly List<RequestLine> _requestLines = [];

    public IncomingMaterialTests()
    {
        Setup(_orders); Setup(_orderMaterials); Setup(_poLines); Setup(_receiptLines); Setup(_materials); Setup(_orderLines);
        Setup(_omDeliveries); Setup(_poDeliveries); Setup(_norms); Setup(_requestLines);
        _materials.Add(Mat());
        _uow.Repository<OrderMaterial>().AddAsync(Arg.Do<OrderMaterial>(om => { om.OrderMaterialId = 99; om.Material = _materials[0]; _orderMaterials.Add(om); })).Returns(Task.CompletedTask);
    }

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private static Material Mat(string code = "VAI01", string name = "Vải cotton", string unit = "m") =>
        new() { MaterialId = 1, MaterialCode = code, MaterialName = name, Unit = unit, Status = MaterialStatuses.Active, MaterialType = "FABRIC" };

    private CustomerOrder Order(int id, string code, string status = OrderStatuses.New, string material = OrderMaterialStatuses.Awaiting,
        int dueInDays = 10)
    {
        var o = new CustomerOrder
        {
            OrderId = id, OrderCode = code, Status = status, MaterialStatus = material, DueDate = new DateOnly(2026, 11, 1).AddDays(dueInDays),
            Customer = new Customer { CustomerName = "Công ty ABC" },
        };
        _orders.Add(o);
        return o;
    }

    private OrderMaterial OrderMat(CustomerOrder o, int id, decimal customerQty, decimal? purchaseQty = null, short? deliveries = null)
    {
        var om = new OrderMaterial
        {
            OrderMaterialId = id, OrderId = o.OrderId, Order = o, MaterialId = 1, Material = Mat(),
            CustomerQty = customerQty, PurchaseQty = purchaseQty, ExpectedDeliveryCount = deliveries,
        };
        o.OrderMaterials.Add(om);
        _orderMaterials.Add(om);
        return om;
    }

    private void Received(int orderMaterialId, decimal qty, string status = ReceiptStatuses.Confirmed, int? poLineId = null) =>
        _receiptLines.Add(new MaterialReceiptLine
        {
            MaterialReceiptLineId = _receiptLines.Count + 1,
            OrderMaterialId = poLineId is null ? orderMaterialId : null,
            PurchaseOrderLineId = poLineId,
            AcceptedQty = qty,
            MaterialReceipt = new MaterialReceipt { Status = status },
        });

    private OrderMaterialService OrderMaterials() => new(_uow, _notifier);
    private IncomingMaterialService Incoming() => new(_uow, new MaterialSupplyUpdater(_uow));

    // ---------- Đơn chờ khai báo NVL ----------

    [Fact]
    public async Task A_new_order_without_any_declared_nvl_is_listed_as_waiting()
    {
        Order(1, "DH-NEW");

        var result = await OrderMaterials().GetPendingOrdersAsync(new PendingMaterialOrderQuery(), default);

        var row = result.Data!.Items.Single();
        row.OrderCode.Should().Be("DH-NEW");
        row.MaterialLines.Should().Be(0);
        row.Status.Should().Be("New");
        row.MaterialStatus.Should().Be("Awaiting");
        row.CustomerName.Should().Be("Công ty ABC");
    }

    [Fact]
    public async Task Pending_orders_sum_what_the_customer_supplies_and_what_must_be_bought_and_sort_by_due_date()
    {
        var late = Order(1, "DH-LATE", dueInDays: 20);
        var soon = Order(2, "DH-SOON", status: OrderStatuses.InProduction, material: OrderMaterialStatuses.Partial, dueInDays: 5);
        OrderMat(late, 1, 100, 50);
        OrderMat(late, 2, 40, null);
        OrderMat(soon, 3, 0, 300);

        var rows = (await OrderMaterials().GetPendingOrdersAsync(new PendingMaterialOrderQuery(), default)).Data!.Items;

        rows.Select(r => r.OrderCode).Should().Equal("DH-SOON", "DH-LATE");
        rows[1].MaterialLines.Should().Be(2);
        rows[1].CustomerQty.Should().Be(140);
        rows[1].ToBuyQty.Should().Be(50);
        rows[0].ToBuyQty.Should().Be(300);
    }

    [Fact]
    public async Task Pending_orders_leave_out_completed_closed_and_fully_supplied_orders()
    {
        Order(1, "DH-DONE", status: OrderStatuses.Completed, material: OrderMaterialStatuses.Complete);
        Order(2, "DH-CANCELLED", status: OrderStatuses.Cancelled);
        Order(3, "DH-FULL", status: OrderStatuses.InProduction, material: OrderMaterialStatuses.Complete);
        Order(4, "DH-OK");

        (await OrderMaterials().GetPendingOrdersAsync(new PendingMaterialOrderQuery(), default)).Data!.Items.Select(r => r.OrderCode)
            .Should().Equal("DH-OK");
    }

    private async Task<IEnumerable<string>> PendingCodes(PendingMaterialOrderQuery query) =>
        (await OrderMaterials().GetPendingOrdersAsync(query, default)).Data!.Items.Select(r => r.OrderCode);

    [Fact]
    public async Task Pending_orders_put_undeclared_orders_first_even_when_their_due_date_is_later()
    {
        OrderMat(Order(1, "DH-CO", dueInDays: 3), 1, 10, null);
        Order(2, "DH-CHUA", dueInDays: 30);

        (await PendingCodes(new PendingMaterialOrderQuery())).Should().Equal("DH-CHUA", "DH-CO");
    }

    [Theory]
    [InlineData(PendingMaterialStates.NotDeclared, "DH-CHUA")]
    [InlineData(PendingMaterialStates.Awaiting, "DH-CHO")]
    [InlineData(PendingMaterialStates.Partial, "DH-MOTPHAN")]
    [InlineData(PendingMaterialStates.Complete, "DH-DU")]
    public async Task Pending_orders_filter_by_material_state(string state, string expected)
    {
        Order(1, "DH-CHUA");
        OrderMat(Order(2, "DH-CHO"), 1, 10, null);
        OrderMat(Order(3, "DH-MOTPHAN", status: OrderStatuses.InProduction, material: OrderMaterialStatuses.Partial), 2, 10, null);
        OrderMat(Order(4, "DH-DU", status: OrderStatuses.InProduction, material: OrderMaterialStatuses.Complete), 3, 10, null);

        (await PendingCodes(new PendingMaterialOrderQuery { MaterialState = state })).Should().Equal(expected);
    }

    [Fact]
    public async Task Pending_orders_search_by_order_code_and_customer_and_page_on_the_server()
    {
        Order(1, "DH-A");
        Order(2, "DH-B");
        Order(3, "DH-C");

        (await PendingCodes(new PendingMaterialOrderQuery { Keyword = " DH-B " })).Should().Equal("DH-B");
        (await PendingCodes(new PendingMaterialOrderQuery { Keyword = "Công ty ABC" })).Should().HaveCount(3);
        var page = (await OrderMaterials().GetPendingOrdersAsync(new PendingMaterialOrderQuery { Page = 2, PageSize = 2 }, default)).Data!;
        page.TotalItems.Should().Be(3);
        page.Items.Should().ContainSingle();
    }

    // ---------- Bảng NVL của đơn ----------

    private async Task<PagedResult<OrderMaterialDto>> Paged(OrderMaterialPageQuery query) =>
        (await OrderMaterials().GetPagedAsync(query, default)).Data!;

    private (CustomerOrder Order, OrderMaterial Customer, OrderMaterial Buy) TwoLines()
    {
        var o = Order(1, "DH-1");
        var customer = OrderMat(o, 1, 100);
        var buy = OrderMat(o, 2, 0, 50);
        customer.RequiredQty = 100;
        buy.RequiredQty = 50;
        buy.Material = new Material { MaterialId = 2, MaterialCode = "CHI01", MaterialName = "Chỉ may", Color = "Xanh", Unit = "cuộn" };
        return (o, customer, buy);
    }

    [Theory]
    [InlineData(OrderMaterialSupplies.Customer, 1)]
    [InlineData(OrderMaterialSupplies.Purchase, 2)]
    public async Task Order_material_table_filters_by_supply(string supply, int expectedId)
    {
        TwoLines();

        var page = await Paged(new OrderMaterialPageQuery { OrderId = 1, Supply = supply });

        page.Items.Should().ContainSingle(m => m.OrderMaterialId == expectedId);
    }

    [Fact]
    public async Task Order_material_table_filters_by_receipt_state()
    {
        TwoLines();
        Received(1, 100);
        _receiptLines[^1].MaterialReceipt.OrderId = 1;

        (await Paged(new OrderMaterialPageQuery { OrderId = 1, ReceiptState = OrderMaterialReceiptStates.Done }))
            .Items.Should().ContainSingle(m => m.OrderMaterialId == 1);
        (await Paged(new OrderMaterialPageQuery { OrderId = 1, ReceiptState = OrderMaterialReceiptStates.Pending }))
            .Items.Should().ContainSingle(m => m.OrderMaterialId == 2);
    }

    [Theory]
    [InlineData("chi01")]
    [InlineData("CHỈ MAY")]
    [InlineData("xanh")]
    public async Task Order_material_table_searches_code_name_and_color_ignoring_case(string keyword)
    {
        TwoLines();

        (await Paged(new OrderMaterialPageQuery { OrderId = 1, Keyword = keyword }))
            .Items.Should().ContainSingle(m => m.OrderMaterialId == 2);
    }

    [Fact]
    public async Task Order_material_table_pages_and_reports_the_filtered_total()
    {
        TwoLines();

        var page = await Paged(new OrderMaterialPageQuery { OrderId = 1, Page = 2, PageSize = 1 });

        page.TotalItems.Should().Be(2);
        page.Items.Should().ContainSingle();
    }

    // ---------- NVL chờ nhận: khách cấp ----------

    [Fact]
    public async Task Customer_material_waits_until_the_confirmed_receipts_cover_what_the_customer_will_supply()
    {
        var order = Order(1, "DH001");
        OrderMat(order, 10, 500, deliveries: 3);
        _omDeliveries.Add(new VwOrderMaterialDelivery { OrderMaterialId = 10, OrderId = 1, ReceivedCount = 1 });
        Received(10, 200);                                   // phiếu đã xác nhận
        Received(10, 999, ReceiptStatuses.Pending);          // phiếu chờ xác nhận không tính

        var row = (await Incoming().GetAsync(default)).Data!.Customer.Single();

        row.OrderCode.Should().Be("DH001");
        row.CustomerQty.Should().Be(500);
        row.ReceivedQty.Should().Be(200);
        row.OutstandingQty.Should().Be(300);
        row.ExpectedDeliveryCount.Should().Be(3);
        row.ReceivedCount.Should().Be(1);
        row.MaterialName.Should().Be("Vải cotton");
    }

    [Fact]
    public async Task Customer_material_disappears_once_fully_received_and_ignores_zero_or_closed_orders()
    {
        var a = Order(1, "DH-A"); OrderMat(a, 10, 100); Received(10, 100);
        var b = Order(2, "DH-B"); OrderMat(b, 11, 0, 80);                       // khách không cấp gì
        var c = Order(3, "DH-C", status: OrderStatuses.Cancelled); OrderMat(c, 12, 50);   // đơn đã hủy
        var d = Order(4, "DH-D"); OrderMat(d, 13, 50, deliveries: 1);

        (await Incoming().GetAsync(default)).Data!.Customer.Select(r => r.OrderCode).Should().Equal("DH-D");
    }

    [Fact]
    public async Task Customer_material_is_sorted_by_order_then_material_code()
    {
        var late = Order(2, "DH-B");
        var early = Order(1, "DH-A");
        OrderMat(late, 10, 10, deliveries: 1);
        OrderMat(early, 11, 10, deliveries: 2);

        (await Incoming().GetAsync(default)).Data!.Customer.Select(r => r.OrderMaterialId).Should().Equal(11, 10);
    }

    // ---------- NVL chờ nhận: đơn mua ----------

    private PurchaseOrderLine PoLine(int id, string goodsStatus, decimal ordered, decimal notReplenished = 0, string poCode = "PO-001",
        int poId = 1)
    {
        var line = new PurchaseOrderLine
        {
            PurchaseOrderLineId = id, PurchaseOrderId = poId, OrderedQty = ordered, NotReplenishedQty = notReplenished,
            Material = Mat("KHOA", "Khóa kéo", "cái"),
            PurchaseOrder = new PurchaseOrder
            {
                PurchaseOrderId = poId, PoCode = poCode, GoodsStatus = goodsStatus, ExpectedDeliveryCount = 2,
                Supplier = new Supplier { SupplierName = "NCC Hà Nội" },
            },
        };
        _poLines.Add(line);
        return line;
    }

    [Fact]
    public async Task Placed_purchase_orders_wait_for_the_outstanding_quantity_after_confirmed_receipts_and_closed_shortfalls()
    {
        PoLine(1, PurchaseGoodsStatuses.PartiallyReceived, ordered: 1000, notReplenished: 100);
        _poDeliveries.Add(new VwPurchaseOrderDelivery { PurchaseOrderId = 1, ReceivedCount = 1 });
        Received(0, 300, poLineId: 1);
        Received(0, 50, ReceiptStatuses.Returned, poLineId: 1);   // phiếu bị trả: không tính

        var row = (await Incoming().GetAsync(default)).Data!.Supplier.Single();

        row.PoCode.Should().Be("PO-001");
        row.SupplierName.Should().Be("NCC Hà Nội");
        row.OrderedQty.Should().Be(1000);
        row.ReceivedQty.Should().Be(300);
        row.OutstandingQty.Should().Be(600);   // 1000 − 300 − 100 chốt không bù
        row.MaterialName.Should().Be("Khóa kéo");
        row.ExpectedDeliveryCount.Should().Be(2);
        row.ReceivedCount.Should().Be(1);
    }

    [Fact]
    public async Task Purchase_orders_not_yet_placed_or_already_settled_do_not_wait_for_goods()
    {
        PoLine(1, PurchaseOrderStatuses.PendingFinalisation, 100, poCode: "PO-DRAFT", poId: 1);
        PoLine(2, PurchaseGoodsStatuses.Received, 100, poCode: "PO-DONE", poId: 2);
        PoLine(3, PurchaseGoodsStatuses.ClosedShort, 100, poCode: "PO-SHORT", poId: 3);
        PoLine(4, PurchaseGoodsStatuses.Rejected, 100, poCode: "PO-REJ", poId: 4);
        PoLine(5, PurchaseGoodsStatuses.Ordered, 100, poCode: "PO-OPEN", poId: 5);
        PoLine(6, PurchaseGoodsStatuses.Ordered, 100, poCode: "PO-FULL", poId: 6);
        Received(0, 100, poLineId: 6);   // đã nhận đủ dù trạng thái chưa kịp đổi

        (await Incoming().GetAsync(default)).Data!.Supplier.Select(r => r.PoCode).Should().Equal("PO-OPEN");
    }

    // ---------- Thông báo cho Thủ kho khi Quản đốc khai báo NVL khách cấp ----------

    [Fact]
    public async Task Declaring_customer_supplied_nvl_tells_the_warehouse_keeper_what_when_and_where_to_look()
    {
        var order = Order(1, "DH001");

        var result = await OrderMaterials().CreateAsync(
            new CreateOrderMaterialDto(1, 1, 500, 500, (short)3, null), default);

        result.IsSuccess.Should().BeTrue();
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper,
            Arg.Is<NotificationMessage>(m => m.LinkUrl == "/warehouse/incoming-materials" && m.Message.Contains("DH001")
                && m.Message.Contains("500") && m.Message.Contains("3 lần") && m.Message.Contains("Vải cotton")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Declaring_nvl_the_company_will_buy_does_not_notify_the_warehouse_keeper_yet()
    {
        Order(1, "DH001");

        var result = await OrderMaterials().CreateAsync(new CreateOrderMaterialDto(1, 1, 500, 0, null, null), default);

        result.IsSuccess.Should().BeTrue();
        await _notifier.DidNotReceive().NotifyRoleAsync(Arg.Any<string>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }
}
