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

    // ---------- NVL theo mã hàng trong đơn ----------

    private void OrderedVariant(int orderId, int variantId, int productId = 1, string code = "AO-01") =>
        _orderLines.Add(new OrderLine
        {
            OrderLineId = _orderLines.Count + 1, OrderId = orderId, ProductId = productId, ProductVariantId = variantId,
            ProductVariant = new ProductVariant
            {
                ProductVariantId = variantId, ProductId = productId, ProductColor = new ProductColor { Color = "Đỏ" }, SizeCode = "M",
                Product = new Product { ProductId = productId, ProductCode = code, ProductName = "Áo " + code },
            },
        });

    private void Norm(int variantId, Material material, int productId = 1, string componentStatus = RecordStatus.Active) =>
        _norms.Add(new ProductComponentMaterial
        {
            ProductComponentMaterialId = _norms.Count + 1, ProductId = productId, ProductVariantId = variantId,
            MaterialId = material.MaterialId, Material = material,
            ProductComponent = new ProductComponent { Status = componentStatus },
        });

    private static Material OtherMat(int id, string code, string status = MaterialStatuses.Active) =>
        new() { MaterialId = id, MaterialCode = code, MaterialName = code, Unit = "m", Color = "", Status = status, MaterialType = "FABRIC" };

    [Fact]
    public async Task GetProductMaterials_OrderNotFound_ReturnsNotFound()
    {
        var result = await OrderMaterials().GetProductMaterialsAsync(404, default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task GetProductMaterials_SameMaterialOnManyVariants_ListedOnce()
    {
        Order(1, "DH-1");
        OrderedVariant(1, 10);
        OrderedVariant(1, 11);
        var fabric = OtherMat(5, "VAI05");
        Norm(10, fabric);
        Norm(11, fabric);

        var products = (await OrderMaterials().GetProductMaterialsAsync(1, default)).Data!;

        products.Should().ContainSingle();
        products[0].ProductCode.Should().Be("AO-01");
        products[0].Materials.Select(m => m.MaterialId).Should().Equal(5);
    }

    [Fact]
    public async Task GetProductMaterials_VariantNotOrdered_MaterialExcluded()
    {
        Order(1, "DH-1");
        OrderedVariant(1, 10);
        Norm(10, OtherMat(5, "VAI05"));
        Norm(99, OtherMat(6, "VAI06"));

        var products = (await OrderMaterials().GetProductMaterialsAsync(1, default)).Data!;

        products[0].Materials.Select(m => m.MaterialId).Should().Equal(5);
    }

    [Fact]
    public async Task GetProductMaterials_DiscontinuedMaterialOrComponent_Excluded()
    {
        Order(1, "DH-1");
        OrderedVariant(1, 10);
        Norm(10, OtherMat(5, "VAI05", MaterialStatuses.Discontinued));
        Norm(10, OtherMat(6, "VAI06"), componentStatus: RecordStatus.Discontinued);

        var products = (await OrderMaterials().GetProductMaterialsAsync(1, default)).Data!;

        products[0].Materials.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProductMaterials_ProductWithoutNorm_ReturnedWithEmptyMaterials()
    {
        Order(1, "DH-1");
        OrderedVariant(1, 10);
        OrderedVariant(1, 20, 2, "AO-02");
        Norm(20, OtherMat(5, "VAI05"), 2);

        var products = (await OrderMaterials().GetProductMaterialsAsync(1, default)).Data!;

        products.Select(p => p.ProductCode).Should().Equal("AO-01", "AO-02");
        products[0].Materials.Should().BeEmpty();
        products[1].Materials.Should().ContainSingle();
    }

    // ---------- Thêm nhiều NVL cùng lúc ----------

    private static CreateOrderMaterialLineDto BatchLine(int materialId = 1, decimal required = 10, decimal customer = 0, short? count = null) =>
        new(materialId, required, customer, count, null);

    [Fact]
    public async Task CreateBatch_ValidLines_AddsAllAndSavesOnce()
    {
        Order(1, "DH-1");
        _materials.Add(OtherMat(2, "CHI02"));

        var result = await OrderMaterials().CreateBatchAsync(new CreateOrderMaterialBatchDto(1, [BatchLine(), BatchLine(2)]), default);

        result.IsSuccess.Should().BeTrue();
        await _uow.Repository<OrderMaterial>().Received(2).AddAsync(Arg.Any<OrderMaterial>());
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBatch_OneLineAlreadyInOrder_SavesNothing()
    {
        var o = Order(1, "DH-1");
        OrderMat(o, 1, 0);
        _materials.Add(OtherMat(2, "CHI02"));

        var result = await OrderMaterials().CreateBatchAsync(new CreateOrderMaterialBatchDto(1, [BatchLine(2), BatchLine()]), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderMaterialDuplicate);
        await _uow.Repository<OrderMaterial>().DidNotReceive().AddAsync(Arg.Any<OrderMaterial>());
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBatch_OrderClosed_Fails()
    {
        Order(1, "DH-1", OrderStatuses.Closed.First());

        var result = await OrderMaterials().CreateBatchAsync(new CreateOrderMaterialBatchDto(1, [BatchLine()]), default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task CreateBatch_OrderNotFound_ReturnsNotFound()
    {
        var result = await OrderMaterials().CreateBatchAsync(new CreateOrderMaterialBatchDto(404, [BatchLine()]), default);

        result.Kind.Should().Be(ResultKind.NotFound);
    }

    [Fact]
    public async Task CreateBatch_CustomerSupplyLine_NotifiesWarehouseKeeperOnlyForThatLine()
    {
        Order(1, "DH-1");
        _materials.Add(OtherMat(2, "CHI02"));

        await OrderMaterials().CreateBatchAsync(new CreateOrderMaterialBatchDto(1, [BatchLine(customer: 5, count: 1), BatchLine(2)]), default);

        await _notifier.Received(1).NotifyRoleAsync(RoleCodes.WarehouseKeeper, Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
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
