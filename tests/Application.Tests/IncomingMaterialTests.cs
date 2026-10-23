using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Application.Tests;

/// <summary>
/// Đơn chờ khai báo NVL (Quản đốc / Admin) và màn NVL chờ nhận (Thủ kho): NVL khách sẽ cấp và đơn mua đã đặt mà kho chưa nhận đủ.
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

    public IncomingMaterialTests()
    {
        Setup(_orders); Setup(_orderMaterials); Setup(_poLines); Setup(_receiptLines); Setup(_materials); Setup(_orderLines);
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

    private OrderMaterial OrderMat(CustomerOrder o, int id, decimal customerQty, decimal? purchaseQty = null, DateOnly? expected = null)
    {
        var om = new OrderMaterial
        {
            OrderMaterialId = id, OrderId = o.OrderId, Order = o, MaterialId = 1, Material = Mat(), MaterialColor = "Đen",
            CustomerQty = customerQty, PurchaseQty = purchaseQty, CustomerExpectedDate = expected,
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
    private IncomingMaterialService Incoming() => new(_uow);

    // ---------- Đơn chờ khai báo NVL ----------

    [Fact]
    public async Task A_new_order_without_any_declared_nvl_is_listed_as_waiting()
    {
        Order(1, "DH-NEW");

        var result = await OrderMaterials().GetPendingOrdersAsync(default);

        var row = result.Data!.Single();
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

        var rows = (await OrderMaterials().GetPendingOrdersAsync(default)).Data!;

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

        (await OrderMaterials().GetPendingOrdersAsync(default)).Data!.Select(r => r.OrderCode).Should().Equal("DH-OK");
    }

    // ---------- NVL chờ nhận: khách cấp ----------

    [Fact]
    public async Task Customer_material_waits_until_the_confirmed_receipts_cover_what_the_customer_will_supply()
    {
        var order = Order(1, "DH001");
        OrderMat(order, 10, 500, expected: new DateOnly(2026, 10, 30));
        Received(10, 200);                                   // phiếu đã xác nhận
        Received(10, 999, ReceiptStatuses.Pending);          // phiếu chờ xác nhận không tính

        var row = (await Incoming().GetAsync(default)).Data!.Customer.Single();

        row.OrderCode.Should().Be("DH001");
        row.CustomerQty.Should().Be(500);
        row.ReceivedQty.Should().Be(200);
        row.OutstandingQty.Should().Be(300);
        row.ExpectedDate.Should().Be(new DateOnly(2026, 10, 30));
        row.MaterialName.Should().Be("Vải cotton");
    }

    [Fact]
    public async Task Customer_material_disappears_once_fully_received_and_ignores_zero_or_closed_orders()
    {
        var a = Order(1, "DH-A"); OrderMat(a, 10, 100); Received(10, 100);
        var b = Order(2, "DH-B"); OrderMat(b, 11, 0, 80);                       // khách không cấp gì
        var c = Order(3, "DH-C", status: OrderStatuses.Cancelled); OrderMat(c, 12, 50);   // đơn đã hủy
        var d = Order(4, "DH-D"); OrderMat(d, 13, 50, expected: new DateOnly(2026, 10, 25));

        (await Incoming().GetAsync(default)).Data!.Customer.Select(r => r.OrderCode).Should().Equal("DH-D");
    }

    [Fact]
    public async Task Customer_material_is_sorted_by_the_promised_date_with_undated_last()
    {
        var o = Order(1, "DH001");
        OrderMat(o, 10, 10);
        OrderMat(o, 11, 10, expected: new DateOnly(2026, 11, 5));
        OrderMat(o, 12, 10, expected: new DateOnly(2026, 11, 1));

        (await Incoming().GetAsync(default)).Data!.Customer.Select(r => r.OrderMaterialId).Should().Equal(12, 11, 10);
    }

    // ---------- NVL chờ nhận: đơn mua ----------

    private PurchaseOrderLine PoLine(int id, string goodsStatus, decimal ordered, decimal notReplenished = 0, string poCode = "PO-001",
        int poId = 1)
    {
        var line = new PurchaseOrderLine
        {
            PurchaseOrderLineId = id, PurchaseOrderId = poId, OrderedQty = ordered, NotReplenishedQty = notReplenished,
            Material = Mat("KHOA", "Khóa kéo", "cái"), MaterialColor = "Đen",
            PurchaseOrder = new PurchaseOrder
            {
                PurchaseOrderId = poId, PoCode = poCode, GoodsStatus = goodsStatus, ExpectedDeliveryDate = new DateOnly(2026, 10, 28),
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
        Received(0, 300, poLineId: 1);
        Received(0, 50, ReceiptStatuses.Returned, poLineId: 1);   // phiếu bị trả: không tính

        var row = (await Incoming().GetAsync(default)).Data!.Supplier.Single();

        row.PoCode.Should().Be("PO-001");
        row.SupplierName.Should().Be("NCC Hà Nội");
        row.OrderedQty.Should().Be(1000);
        row.ReceivedQty.Should().Be(300);
        row.OutstandingQty.Should().Be(600);   // 1000 − 300 − 100 chốt không bù
        row.MaterialName.Should().Be("Khóa kéo");
        row.ExpectedDeliveryDate.Should().Be(new DateOnly(2026, 10, 28));
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
            new CreateOrderMaterialDto(1, 1, "Đen", 500, 500, new DateOnly(2026, 10, 30), null, null), default);

        result.IsSuccess.Should().BeTrue();
        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper,
            Arg.Is<NotificationMessage>(m => m.LinkUrl == "/warehouse/incoming-materials" && m.Message.Contains("DH001")
                && m.Message.Contains("500") && m.Message.Contains("30/10/2026") && m.Message.Contains("Vải cotton")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Declaring_nvl_the_company_will_buy_does_not_notify_the_warehouse_keeper_yet()
    {
        Order(1, "DH001");

        var result = await OrderMaterials().CreateAsync(new CreateOrderMaterialDto(1, 1, "Đen", 500, 0, null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        await _notifier.DidNotReceive().NotifyRoleAsync(Arg.Any<string>(), Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }
}
