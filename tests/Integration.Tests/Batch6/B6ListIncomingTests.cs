using ClosedXML.Excel;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>IT_B6_088–093, 095–098 — IncomingMaterials, export Excel, danh sách + phân trang.</summary>
public sealed class B6ListIncomingTests(ITestOutputHelper output) : B6TestBase(output)
{
    private static List<System.Text.Json.JsonElement> Arr6(Api r, string name) => Arr(r.Data, name);

    private static System.Text.Json.JsonElement RowOf(List<System.Text.Json.JsonElement> rows, string key, int id) =>
        rows.Single(x => B1Json.Int(x, key) == id);

    private async Task<decimal> ConfirmedQtyAsync(Func<MaterialReceiptLine, bool> pick)
    {
        await using var db = NewDb();
        var lines = await db.MaterialReceiptLines.AsNoTracking().Include(l => l.MaterialReceipt).Where(l => l.MaterialReceipt.Status == ReceiptStatuses.Confirmed).ToListAsync();
        return lines.Where(pick).Sum(l => l.AcceptedQty);
    }

    [Fact]
    public async Task IT_B6_088_Incoming_Materials_Matches_Outstanding_From_Db()
    {
        var before = await Snap6();

        var r = await Call("warehouse", Get, INC);

        r.Code.Should().Be(200, r.ToString());
        var customer = Arr6(r, "customer");
        var supplier = Arr6(r, "supplier");
        var closed = Arr6(r, "closedShort");
        var om2 = RowOf(customer, "orderMaterialId", R.Om2.OrderMaterialId);
        B1Json.Dec(om2, "customerQty").Should().Be(200m);
        B1Json.Dec(om2, "outstandingQty").Should().Be(200m - await ConfirmedQtyAsync(l => l.OrderMaterialId == R.Om2.OrderMaterialId));
        customer.Select(x => B1Json.Int(x, "orderMaterialId")).Should().Contain(R.Om1.OrderMaterialId);
        var pol1 = RowOf(supplier, "purchaseOrderLineId", R.Pol1.PurchaseOrderLineId);
        B1Json.Dec(pol1, "orderedQty").Should().Be(300m);
        B1Json.Dec(pol1, "receivedQty").Should().Be(200m);
        B1Json.Dec(pol1, "outstandingQty").Should().Be(100m);
        supplier.Select(x => B1Json.Int(x, "purchaseOrderLineId")).Should().Contain(R.Pol3.PurchaseOrderLineId);
        var cs = RowOf(closed, "purchaseOrderLineId", R.Pol6.PurchaseOrderLineId);
        B1Json.Dec(cs, "notReplenishedQty").Should().Be(10m);
        Note($"Phiếu PENDING có được tính vào đã nhận? Om1 outstanding = {B1Json.Dec(RowOf(customer, "orderMaterialId", R.Om1.OrderMaterialId), "outstandingQty")}");
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_089_Confirmed_Receipt_Reduces_Outstanding()
    {
        (await Confirm(R.Mr1.MaterialReceiptId)).Code.Should().Be(200);

        var r = await Call("warehouse", Get, INC);

        r.Code.Should().Be(200, r.ToString());
        var om1 = RowOf(Arr6(r, "customer"), "orderMaterialId", R.Om1.OrderMaterialId);
        B1Json.Dec(om1, "outstandingQty").Should().Be(150m - 100m);
        B1Json.Dec(om1, "receivedQty").Should().Be(100m);
    }

    [Fact]
    public async Task IT_B6_090_Returned_Receipt_Does_Not_Reduce_Outstanding()
    {
        (await ReturnMr(R.Mr1.MaterialReceiptId, new { Reason = "Sai số lượng" })).Code.Should().Be(200);

        var r = await Call("warehouse", Get, INC);

        r.Code.Should().Be(200, r.ToString());
        var om1 = RowOf(Arr6(r, "customer"), "orderMaterialId", R.Om1.OrderMaterialId);
        B1Json.Dec(om1, "outstandingQty").Should().Be(150m);
    }

    [Fact]
    public async Task IT_B6_091_Filters_And_Computed_Columns_Match()
    {
        var r = await Call("manager", Get, $"{MRM}?OrderId={R.Od6.OrderId}&Source=CUSTOMER");

        r.Code.Should().Be(200, r.ToString());
        var ids = IdsOf(r, "materialReceiptId");
        ids.Should().Contain([R.Mr1.MaterialReceiptId, R.Mr4.MaterialReceiptId]).And.NotContain(R.Mr2.MaterialReceiptId);
        await using var db = NewDb();
        var om2 = await db.OrderMaterials.AsNoTracking().SingleAsync(x => x.OrderMaterialId == R.Om2.OrderMaterialId);
        om2.PurchaseQty.Should().Be(300m);
        om2.Source.Should().Be("BOTH");
        (await db.OrderMaterials.AsNoTracking().SingleAsync(x => x.OrderMaterialId == R.Om1.OrderMaterialId)).Source.Should().Be("CUSTOMER");
        var receipts = await db.MaterialReceipts.AsNoTracking().Where(x => ids.Contains(x.MaterialReceiptId)).ToListAsync();
        receipts.Should().OnlyContain(x => x.PurchaseOrderId == null && x.Source == "CUSTOMER");
    }

    [Theory]
    [InlineData("IT_B6_092", "manager")]
    [InlineData("IT_B6_092", "warehouse")]
    public async Task IT_B6_092_Excel_Export_Returns_Openable_Workbook(string caseId, string who)
    {
        var before = await Snap6();
        var prefix = who == "manager" ? MRM : MRW;

        var res = await RawAsync($"{prefix}/{R.Mr1.MaterialReceiptId}/excel", who);

        res.Code.Should().Be(200, res.Text);
        res.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using var wb = new XLWorkbook(new MemoryStream(res.Bytes));
        var cells = wb.Worksheets.SelectMany(w => w.CellsUsed()).Select(c => c.GetString()).ToList();
        cells.Should().NotBeEmpty();
        cells.Should().Contain(c => c.Contains(R.Mr1.ReceiptCode) || c.Contains(B.Ma2.MaterialCode), "file in ra phải chứa mã phiếu hoặc mã NVL của phiếu");
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_093", "missing")]
    [InlineData("IT_B6_093", "abc")]
    public async Task IT_B6_093_Excel_Of_Unknown_Receipt_Returns_Json_404(string caseId, string kind)
    {
        var id = kind == "abc" ? "abc" : (await MissingMrIdAsync()).ToString();

        var r = await Call("manager", Get, $"{MRM}/{id}/excel");

        Graceful(r, 404);
        if (kind == "missing") r.IsJson.Should().BeTrue("lỗi trả JSON ApiFailure, không trả file rỗng");
    }

    [Theory]
    [InlineData("IT_B6_095", "PENDING")]
    [InlineData("IT_B6_095", "Pending")]
    [InlineData("IT_B6_095", "Xyz")]
    public async Task IT_B6_095_Status_Filter(string caseId, string status)
    {
        var r = await Call("manager", Get, $"{MRM}?Status={status}");

        if (status == "Xyz") Graceful(r, 200, 400); else Graceful(r, 200);
        if (r.Code != 200) return;
        var ids = IdsOf(r, "materialReceiptId");
        await using var db = NewDb();
        if (status == "PENDING")
        {
            var expected = await db.MaterialReceipts.AsNoTracking().Where(x => x.Status == "PENDING").Select(x => x.MaterialReceiptId).ToListAsync();
            ids.Should().BeEquivalentTo(expected);
            ids.Should().Contain([R.Mr1.MaterialReceiptId, R.Mr2.MaterialReceiptId]).And.NotContain([R.Mr3.MaterialReceiptId, R.Mr4.MaterialReceiptId]);
        }
        else Note($"Status={status} -> {ids.Count} phiếu");
    }

    [Fact]
    public async Task IT_B6_096_Source_And_Parent_Filters()
    {
        var sup = await Call("manager", Get, $"{MRM}?Source=SUPPLIER&PurchaseOrderId={R.Po2.PurchaseOrderId}");
        var byOrder = await Call("manager", Get, $"{MRM}?OrderId={R.Od6.OrderId}");
        var lower = await Call("manager", Get, $"{MRM}?Source=supplier");

        sup.Code.Should().Be(200, sup.ToString());
        IdsOf(sup, "materialReceiptId").Should().BeEquivalentTo([R.Mr2.MaterialReceiptId]);
        byOrder.Code.Should().Be(200, byOrder.ToString());
        IdsOf(byOrder, "materialReceiptId").Should().BeEquivalentTo([R.Mr1.MaterialReceiptId, R.Mr3.MaterialReceiptId, R.Mr4.MaterialReceiptId]);
        Graceful(lower, 200);
        Note($"Source=supplier (chữ thường) -> {IdsOf(lower, "materialReceiptId").Count} phiếu");
    }

    [Theory]
    [InlineData("IT_B6_097", "0", "1")]
    [InlineData("IT_B6_097", "1", "1")]
    [InlineData("IT_B6_097", "100", "1")]
    [InlineData("IT_B6_097", "101", "1")]
    [InlineData("IT_B6_097", "-1", "1")]
    [InlineData("IT_B6_097", "20", "0")]
    [InlineData("IT_B6_097", "20", "-5")]
    public async Task IT_B6_097_Paging_Boundaries_Are_Consistent_Across_Prefixes(string caseId, string pageSize, string page)
    {
        var before = await Snap6();
        var q = $"?PageSize={pageSize}&Page={page}";

        var m = await Call("manager", Get, MRM + q);
        var w = await Call("warehouse", Get, MRW + q);

        Graceful(m, 200, 400);
        Graceful(w, 200, 400);
        w.Code.Should().Be(m.Code, "hành vi phải nhất quán giữa M/ và WK/");
        if (m.Code == 200) m.Items().Count.Should().BeLessThanOrEqualTo(100);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_098", "kw100")]
    [InlineData("IT_B6_098", "kw101")]
    [InlineData("IT_B6_098", "inject")]
    [InlineData("IT_B6_098", "ASC")]
    [InlineData("IT_B6_098", "xyz")]
    public async Task IT_B6_098_Keyword_And_Sort_Boundaries(string caseId, string kind)
    {
        var before = await Snap6();
        var q = kind switch
        {
            "kw100" => "?Keyword=" + new string('a', 100),
            "kw101" => "?Keyword=" + new string('a', 101),
            "inject" => "?Keyword=" + Uri.EscapeDataString("' OR 1=1--"),
            _ => "?SortDir=" + kind,
        };

        var r = await Call("manager", Get, MRM + q);

        if (kind == "kw101") Graceful(r, 200, 400); else Graceful(r, 200);
        if (kind is "kw100" or "inject") r.Items().Should().BeEmpty();
        (await MrCountAsync()).Should().BeGreaterThan(0, "bảng còn nguyên");
        await Unchanged6(before);
    }
}
