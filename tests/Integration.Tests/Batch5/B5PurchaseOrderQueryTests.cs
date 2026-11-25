using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

public sealed class B5PurchaseOrderQueryTests(ITestOutputHelper output) : B5TestBase(output)
{
    private async Task<Dictionary<int, string>> GoodsOf(IEnumerable<int> ids)
    {
        await using var db = NewDb();
        return await db.PurchaseOrders.AsNoTracking().Where(p => ids.Contains(p.PurchaseOrderId)).ToDictionaryAsync(p => p.PurchaseOrderId, p => p.GoodsStatus);
    }

    private async Task<Dictionary<int, string>> PayOf(IEnumerable<int> ids)
    {
        await using var db = NewDb();
        return await db.PurchaseOrders.AsNoTracking().Where(p => ids.Contains(p.PurchaseOrderId)).ToDictionaryAsync(p => p.PurchaseOrderId, p => p.PaymentStatus);
    }

    [Fact]
    public async Task IT_B5_049_Detail_Shapes_And_Amounts_Match_Db()
    {
        var before = await Snap5();
        var id = P.Po1.PurchaseOrderId;

        var po = await Call("manager", Get, $"{PO}/{id}");
        var trk = await Call("manager", Get, $"{TRK}/{id}");
        var list = await Call("manager", Get, $"{TRK}?keyword=PO-B5-01&pageSize=100");

        po.Code.Should().Be(200, po.ToString());
        trk.Code.Should().Be(200, trk.ToString());
        var lines = Arr(po.Data, "lines");
        lines.Should().HaveCount(2);
        lines.Single(l => B1Json.Str(l, "materialCode") == B.Ma3.MaterialCode).Let(l => B1Json.Str(l, "materialColor").Should().Be(""));
        Arr(trk.Data, "expectedDeliveryDates").Should().HaveCount(2);
        B1Json.Prop(trk.Data, "expectedDeliveryDate").Should().NotBeNull();
        var dbLines = await PolsAsync(id);
        foreach (var l in Arr(trk.Data, "lines"))
            B1Json.Dec(l, "amount").Should().Be(dbLines.Single(x => x.PurchaseOrderLineId == B1Json.Int(l, "purchaseOrderLineId")).Amount);
        var item = list.Items().Single(i => B1Json.Int(i, "purchaseOrderId") == id);
        B1Json.Dec(item, "totalAmount").Should().Be(dbLines.Sum(l => l.Amount));
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_050_Null_MaterialId_Line_Does_Not_Break_Reads()
    {
        var po = new NX_MTD.Domain.Entities.PurchaseOrder
        {
            PoCode = "PO-B5-NULL", SupplierId = B.Su1.SupplierId, GoodsStatus = "ORDERED", PaymentStatus = "UNPAID", OrderDate = T, CreatedAt = Clock.Now,
        };
        Db.PurchaseOrders.Add(po);
        await Db.SaveChangesAsync();
        Db.PurchaseOrderLines.Add(new NX_MTD.Domain.Entities.PurchaseOrderLine { PurchaseOrderId = po.PurchaseOrderId, MaterialId = null, OrderedQty = 1m, UnitPrice = 1m });
        await Db.SaveChangesAsync();
        var before = await Snap5();

        var one = await Call("manager", Get, $"{PO}/{po.PurchaseOrderId}");
        var all = await Call("manager", Get, PO);

        one.Code.Should().Be(200, one.ToString());
        all.Code.Should().Be(200, all.ToString());
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_051", "ORDERED")]
    [InlineData("IT_B5_051", "Ordered")]
    [InlineData("IT_B5_051", "Xyz")]
    public async Task IT_B5_051_GoodsStatus_Filter_On_PurchaseOrders(string caseId, string status)
    {
        var before = await Snap5();

        var r = await Call("manager", Get, $"{PO}?goodsStatus={status}&pageSize=100");

        r.ShouldNotBeServerError();
        if (status == "ORDERED")
        {
            r.Code.Should().Be(200, r.ToString());
            var ids = IdsOf(r);
            ids.Should().Contain([P.Po2.PurchaseOrderId, P.Po3.PurchaseOrderId, P.Po6.PurchaseOrderId]);
            (await GoodsOf(ids)).Values.Should().OnlyContain(s => s == "ORDERED");
        }
        else r.Code.Should().BeOneOf(200, 400);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_052", "goodsStatus=PendingFinalisation")]
    [InlineData("IT_B5_052", "goodsStatus=PENDING_FINALISATION")]
    [InlineData("IT_B5_052", "paymentStatus=Unpaid")]
    public async Task IT_B5_052_Flow_Query_Uses_PascalCase_Filters(string caseId, string query)
    {
        var before = await Snap5();

        var r = await Call("manager", Get, $"{TRK}?{query}&pageSize=100");

        r.ShouldNotBeServerError();
        if (query.EndsWith("PendingFinalisation") || query.StartsWith("paymentStatus"))
        {
            r.Code.Should().Be(200, r.ToString());
            var ids = IdsOf(r);
            if (query.StartsWith("goods")) { ids.Should().Contain(P.Po1.PurchaseOrderId); (await GoodsOf(ids)).Values.Should().OnlyContain(s => s == "PENDING_FINALISATION"); }
            else (await PayOf(ids)).Values.Should().OnlyContain(s => s == "UNPAID");
        }
        else r.Code.Should().BeOneOf(200, 400);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_053", "stockOnly=true")]
    [InlineData("IT_B5_053", "stockOnly=false")]
    [InlineData("IT_B5_053", "receivable=true")]
    public async Task IT_B5_053_StockOnly_And_Receivable_Filters(string caseId, string query)
    {
        var before = await Snap5();

        var r = await Call("manager", Get, $"{PO}?{query}&pageSize=100");

        r.Code.Should().Be(200, r.ToString());
        var ids = IdsOf(r);
        await using var db = NewDb();
        var rows = await db.PurchaseOrders.AsNoTracking().Where(p => ids.Contains(p.PurchaseOrderId)).ToListAsync();
        if (query == "stockOnly=true")
        {
            rows.Should().OnlyContain(p => p.OrderId == null);
            ids.Should().Contain([P.Po2.PurchaseOrderId, P.Po3.PurchaseOrderId]);
        }
        else if (query == "receivable=true") rows.Should().OnlyContain(p => p.GoodsStatus == "ORDERED" || p.GoodsStatus == "PARTIALLY_RECEIVED");
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_054_Manager_Tracking_Ignores_PayableOnly()
    {
        var r = await Call("manager", Get, $"{TRK}?payableOnly=true&pageSize=100");

        r.Code.Should().Be(200, r.ToString());
        IdsOf(r).Should().Contain(P.Po1.PurchaseOrderId);
    }

    [Fact]
    public async Task IT_B5_055_Accountant_List_Forces_PayableOnly()
    {
        var r = await Call("accountant", Get, $"{PAY}?payableOnly=false&pageSize=100");

        r.Code.Should().Be(200, r.ToString());
        IdsOf(r).Should().NotContain([P.Po1.PurchaseOrderId, P.Po4.PurchaseOrderId]);
    }

    [Theory]
    [InlineData("IT_B5_056", "pageSize=0", 20)]
    [InlineData("IT_B5_056", "pageSize=1", 1)]
    [InlineData("IT_B5_056", "pageSize=100", 100)]
    [InlineData("IT_B5_056", "pageSize=101", 100)]
    [InlineData("IT_B5_056", "pageSize=-5", 20)]
    [InlineData("IT_B5_056", "page=0", 20)]
    [InlineData("IT_B5_056", "page=-1", 20)]
    public async Task IT_B5_056_Paging_Is_Clamped(string caseId, string query, int max)
    {
        var before = await Snap5();

        var r = await Call("manager", Get, $"{PO}?{query}");

        r.Code.Should().Be(200, r.ToString());
        r.Items().Count.Should().BeLessThanOrEqualTo(max);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_057", "kw100")]
    [InlineData("IT_B5_057", "kw101")]
    [InlineData("IT_B5_057", "inject")]
    [InlineData("IT_B5_057", "asc")]
    [InlineData("IT_B5_057", "desc")]
    [InlineData("IT_B5_057", "sortSql")]
    public async Task IT_B5_057_Keyword_And_Sort_Handling(string caseId, string kind)
    {
        var before = await Snap5();
        var query = kind switch
        {
            "kw100" => "keyword=" + new string('a', 100),
            "kw101" => "keyword=" + new string('a', 101),
            "inject" => "keyword=" + Uri.EscapeDataString("' OR 1=1--"),
            "asc" => "sortBy=poCode&sortDir=ASC&pageSize=100",
            "desc" => "sortBy=poCode&sortDir=xyz&pageSize=100",
            _ => "sortBy=" + Uri.EscapeDataString("poCode; DROP TABLE PurchaseOrder--"),
        };

        var r = await Call("manager", Get, $"{PO}?{query}");

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        if (kind == "inject") { r.Code.Should().Be(200); r.Items().Should().BeEmpty(); }
        if (kind is "asc" or "desc")
        {
            r.Code.Should().Be(200, r.ToString());
            var codes = r.Items().Select(i => B1Json.Str(i, "poCode")!).ToList();
            codes.Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase, kind == "asc" ? "ASC" : "xyz");
            if (kind == "desc") codes.Should().BeInDescendingOrder(StringComparer.OrdinalIgnoreCase);
        }

        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_058_Missing_Or_NonInteger_Id_Returns_404()
    {
        var before = await Snap5();
        var missing = await MissingPoIdAsync();

        var a = await Call("manager", Get, $"{PO}/{missing}");
        var b = await Call("warehouse", Get, $"{WKP}/abc");
        var c = await Call("accountant", Get, $"{PAY}/{missing}");

        new[] { a, b, c }.Should().OnlyContain(x => x.Code == 404, $"{a} | {b} | {c}");
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_059_Accountant_Detail_Of_Unplaced_Po()
    {
        var before = await Snap5();

        var r = await Call("accountant", Get, $"{PAY}/{P.Po1.PurchaseOrderId}");

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 404);
        Note($"accountant detail of unplaced PO -> {r.Code}");
        await Unchanged(before);
    }
}

internal static class B5Ext
{
    public static void Let<T>(this T value, Action<T> act) => act(value);
}
