using ClosedXML.Excel;
using FluentAssertions;
using NSubstitute;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Services;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Excel;

namespace Application.Tests;

/// <summary>DEV-ALL-119: nhập Excel NVL của đơn (gộp theo mã NVL, lưu một lần) và số đã nhận tách khách giao / mua về.</summary>
[Collection(ExcelTestCollection.Name)]
public class OrderMaterialImportTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly INotificationService _notifier = Substitute.For<INotificationService>();
    private readonly List<CustomerOrder> _orders = [];
    private readonly List<OrderMaterial> _orderMaterials = [];
    private readonly List<Material> _materials = [];
    private readonly List<MaterialReceipt> _receipts = [];
    private readonly List<MaterialReceiptLine> _receiptLines = [];
    private readonly List<PurchaseOrderLine> _purchaseLines = [];
    private readonly CustomerOrder _order = new() { OrderId = 1, OrderCode = "DH1", Status = OrderStatuses.New, CustomerDeliveryCount = 2 };

    public OrderMaterialImportTests()
    {
        Setup(_orders); Setup(_orderMaterials); Setup(_materials); Setup(_receipts); Setup(_receiptLines); Setup(_purchaseLines); Setup(new List<RequestLine>());
        Setup(new List<VwOrderMaterialDelivery>());
        _orders.Add(_order);
        _materials.AddRange([Mat(1, "VAI01"), Mat(2, "CHI01"), Mat(3, "KHOA01"),
            new Material { MaterialId = 4, MaterialCode = "CU01", MaterialName = "Cũ", Color = "", Unit = "m", Status = MaterialStatuses.Discontinued }]);
        _orderMaterials.Add(new OrderMaterial
        {
            OrderMaterialId = 7,
            OrderId = 1,
            Order = _order,
            MaterialId = 1,
            Material = _materials[0],
            RequiredQty = 100,
            CustomerQty = 100,
        });
    }

    private static Material Mat(int id, string code) =>
        new() { MaterialId = id, MaterialCode = code, MaterialName = "NVL " + code, Color = "Đen", Unit = "m", Status = MaterialStatuses.Active };

    private void Setup<T>(List<T> backing) where T : class
    {
        var repo = Substitute.For<IGenericRepository<T>>();
        repo.Query().Returns(_ => backing.AsTestAsyncQueryable());
        _uow.Repository<T>().Returns(repo);
    }

    private OrderMaterialService Sut() => new(_uow, _notifier, new DeliveryCountGuard(_uow), new ClosedXmlOrderMaterialParser());

    private static MemoryStream Book(params object?[][] rows)
    {
        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("NVL của đơn");
        string[] headers = ["Mã NVL", "Số cần", "Khách cấp", "Ghi chú"];
        for (var c = 0; c < headers.Length; c++) ws.Cell(3, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = ws.Cell(4 + r, c + 1);
                switch (rows[r][c])
                {
                    case null: break;
                    case double d: cell.Value = d; break;
                    case int i: cell.Value = i; break;
                    case var v: cell.Value = v.ToString(); break;
                }
            }
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task PreviewImport_ClassifiesRowsAndComputesPurchaseQty()
    {
        using var stream = Book(
            ["VAI01", 100, 100, null],      // giống dòng đang có
            ["vai01", 120, 100, null],      // trùng mã với dòng 4
            ["CHI01", 300, null, "40/2"],   // mới, khách không cấp → phải mua 300
            ["XX99", 10, 0, null],          // mã không có
            ["CU01", 10, 0, null],          // NVL ngừng dùng
            ["KHOA01", 0, 0, null],         // số cần và khách cấp đều 0
            ["KHOA01", "abc", 0, null]);    // không phải số

        var data = (await Sut().PreviewImportAsync(1, stream, default)).Data!;

        data.Rows.Select(r => r.Action).Should().Equal("Unchanged", "Error", "New", "Error", "Error", "Error", "Error");
        data.Rows[1].Error.Should().Contain("dòng 4");
        data.Rows[2].PurchaseQty.Should().Be(300);
        data.Rows[3].Error.Should().Contain("XX99");
        data.Rows[6].Error.Should().Contain("không phải số");
        data.ChangeCount.Should().Be(1);
        data.DuplicateCount.Should().Be(1);
        data.ErrorCount.Should().Be(4);
    }

    [Fact]
    public async Task Import_UpdatesExistingAddsNewKeepsOthersAndSavesOnce()
    {
        _orderMaterials.Add(new OrderMaterial { OrderMaterialId = 8, OrderId = 1, MaterialId = 3, Material = _materials[2], RequiredQty = 5 });
        var added = new List<OrderMaterial>();
        _uow.Repository<OrderMaterial>().When(r => r.AddAsync(Arg.Any<OrderMaterial>())).Do(c => added.Add(c.Arg<OrderMaterial>()));
        using var stream = Book(["VAI01", 150, 100, "thêm 50 m"], ["CHI01", 300, 0, null]);

        var result = await Sut().ImportAsync(1, stream, default);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Created.Should().Be(1);
        result.Data.Updated.Should().Be(1);
        _orderMaterials[0].RequiredQty.Should().Be(150);
        _orderMaterials[0].Note.Should().Be("thêm 50 m");
        _orderMaterials[1].RequiredQty.Should().Be(5);                 // KHOA01 không có trong file: giữ nguyên
        added.Should().ContainSingle().Which.MaterialId.Should().Be(2);
        await _uow.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Import_IncreaseCustomerQtyWhenDeliveriesFull_IsRowError()
    {
        _order.CustomerDeliveryCount = 1;
        _receipts.Add(new MaterialReceipt { MaterialReceiptId = 1, OrderId = 1, Status = ReceiptStatuses.Confirmed });
        using var stream = Book(["VAI01", 150, 150, null]);

        var preview = (await Sut().PreviewImportAsync(1, stream, default)).Data!;

        preview.Rows.Single().Action.Should().Be("Error");
        preview.Rows.Single().Error.Should().Be(string.Format(WarehouseMessages.CustomerDeliveryFull, 1));
    }

    [Fact]
    public async Task Import_ClosedOrder_Fails()
    {
        _order.Status = OrderStatuses.Completed;
        using var stream = Book(["CHI01", 300, 0, null]);

        var result = await Sut().ImportAsync(1, stream, default);

        result.ErrorCode.Should().Be(ErrorCodes.WarehouseOrderClosed);
        await _uow.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task GetByOrder_SplitsReceivedByCustomerAndPurchaseAndComputesShort()
    {
        var fromCustomer = new MaterialReceipt { MaterialReceiptId = 1, OrderId = 1, Status = ReceiptStatuses.Confirmed };
        var fromSupplier = new MaterialReceipt { MaterialReceiptId = 2, OrderId = 1, Status = ReceiptStatuses.Confirmed, PurchaseOrderId = 9 };
        _receiptLines.Add(new MaterialReceiptLine { OrderMaterialId = 7, AcceptedQty = 60, MaterialReceipt = fromCustomer });
        _receiptLines.Add(new MaterialReceiptLine { OrderMaterialId = 7, AcceptedQty = 15, MaterialReceipt = fromSupplier });

        var row = (await Sut().GetByOrderAsync(1, default)).Data!.Single();

        row.CustomerReceivedQty.Should().Be(60);
        row.PurchasedReceivedQty.Should().Be(15);
        row.ReceivedQty.Should().Be(75);
        row.ShortQty.Should().Be(25);
    }

    // ========== Chế độ thay thế ==========

    [Fact]
    public async Task Import_Replace_RemovesOrderMaterialsNotInFile_ExceptThoseWithReceiptsOrPurchases()
    {
        _orderMaterials.Add(new OrderMaterial { OrderMaterialId = 8, OrderId = 1, MaterialId = 3, Material = _materials[2], RequiredQty = 5 });   // KHOA01: xóa được
        _receiptLines.Add(new MaterialReceiptLine { MaterialReceiptLineId = 1, OrderMaterialId = 7, AcceptedQty = 10 });                        // VAI01: đã có phiếu nhập
        using var stream = Book(["CHI01", 300, 0, null]);

        var preview = (await Sut().PreviewImportAsync(1, stream, default, replace: true)).Data!;
        preview.RemoveCount.Should().Be(1);
        preview.Removed.Should().Equal("KHOA01");
        preview.Blocked.Should().Equal("VAI01");
        preview.ChangeCount.Should().Be(2);                     // 1 thêm (CHI01) + 1 xóa (KHOA01)

        using var again = Book(["CHI01", 300, 0, null]);
        var result = await Sut().ImportAsync(1, again, default, replace: true);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Data!.Created.Should().Be(1);
        result.Data.Removed.Should().Be(1);
        _uow.Repository<OrderMaterial>().Received(1).Remove(Arg.Is<OrderMaterial>(o => o.OrderMaterialId == 8));
        result.Message.Should().Contain("Đã xóa 1 NVL");
    }

    [Fact]
    public async Task Import_WithoutReplace_NeverRemoves()
    {
        _orderMaterials.Add(new OrderMaterial { OrderMaterialId = 8, OrderId = 1, MaterialId = 3, Material = _materials[2], RequiredQty = 5 });
        using var stream = Book(["CHI01", 300, 0, null]);

        var result = await Sut().ImportAsync(1, stream, default);

        result.Data!.Removed.Should().Be(0);
        _uow.Repository<OrderMaterial>().DidNotReceive().Remove(Arg.Any<OrderMaterial>());
    }

    [Fact]
    public async Task Import_Replace_FileWithOnlyErrorRows_RemovesNothing()
    {
        _orderMaterials.Add(new OrderMaterial { OrderMaterialId = 8, OrderId = 1, MaterialId = 3, Material = _materials[2], RequiredQty = 5 });
        using var stream = Book(["CHI01", "abc", 0, null]);

        var preview = (await Sut().PreviewImportAsync(1, stream, default, replace: true)).Data!;

        preview.RemoveCount.Should().Be(0);
    }

    [Fact]
    public async Task Import_Replace_ErrorRowMaterialStillCountsAsInFile()
    {
        using var stream = Book(["VAI01", "abc", 100, null], ["CHI01", 300, 0, null]);   // VAI01 lỗi (không phải số) nhưng vẫn có trong file

        var preview = (await Sut().PreviewImportAsync(1, stream, default, replace: true)).Data!;

        preview.RemoveCount.Should().Be(0);
    }
}
