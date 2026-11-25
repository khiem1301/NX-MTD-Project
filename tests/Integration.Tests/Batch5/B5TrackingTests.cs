using Integration.Tests.Batch4;
using Integration.Tests.Batch1;
using System.Globalization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

/// <summary>IT_B5_060–088 (trừ các ca đồng thời / fault-injection nằm ở B5ConcurrencyTests) — place / cancel / reprice.</summary>
public sealed class B5TrackingTests(ITestOutputHelper output) : B5TestBase(output)
{
    private static decimal Dc(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    private string PlaceUrl(int id) => $"{TRK}/{id}/place";
    private string CancelUrl(int id) => $"{TRK}/{id}/cancel";
    private string RepriceUrl(int id) => $"{TRK}/{id}/reprice";

    private async Task<Api> RejectPlace(object body, params int[] codes)
    {
        var before = await Snap5();
        var r = await Call("manager", Post, PlaceUrl(P.Po1.PurchaseOrderId), body);
        Rejected(r, codes);
        await Unchanged(before);
        return r;
    }

    // ------------------------------------------------------------------ place

    [Fact]
    public async Task IT_B5_060_Place_Finalises_Lines_And_Replaces_Schedule()
    {
        var id = P.Po1.PurchaseOrderId;
        var rv = await RvAsync(id);
        var dates = Dates(3);

        var r = await Call("manager", Post, PlaceUrl(id), PlaceBody(rv, Po1Lines(), count: 3, dates: dates));

        r.Code.Should().Be(200, r.ToString());
        var po = await PoAsync(id);
        po.GoodsStatus.Should().Be("ORDERED");
        po.RowVer.Should().NotEqual(rv);
        po.ExpectedDeliveryDate.Should().Be(dates[0]);
        po.ExpectedDeliveryDateTo.Should().Be(dates[2]);
        var l1 = await PolAsync(P.Pol1.PurchaseOrderLineId);
        (l1.OrderedQty, l1.UnitPrice, l1.Amount).Should().Be((110m, 49_500m, 5_445_000.00m));
        var dsd = await PoDsdAsync(id);
        dsd.Select(d => d.SeqNo).Should().Equal(1, 2, 3);
        dsd.Select(d => d.DeliveryDate).Should().Equal(dates);
    }

    [Fact]
    public async Task IT_B5_061_Place_With_Stale_RowVer_Returns_409()
    {
        var id = P.Po1.PurchaseOrderId;
        var stale = await RvAsync(id);
        await BumpAsync(id);
        var before = await Snap5();

        var r = await Call("manager", Post, PlaceUrl(id), PlaceBody(stale, Po1Lines()));

        r.Code.Should().Be(409, r.ToString());
        (await PoDsdAsync(id)).Should().HaveCount(2);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_062", "missing")]
    [InlineData("IT_B5_062", "abc")]
    [InlineData("IT_B5_062", "empty")]
    public async Task IT_B5_062_Place_With_Invalid_RowVer(string caseId, string kind)
    {
        var body = PlaceBody(null, Po1Lines());
        if (kind == "missing") body.Remove("RowVer"); else body["RowVer"] = kind == "abc" ? "abc" : "";

        await RejectPlace(body, 400, 409);
    }

    [Fact]
    public async Task IT_B5_064_Place_With_Line_Of_Another_Po_Is_Rejected()
    {
        var rv = await RvAsync(P.Po1.PurchaseOrderId);

        await RejectPlace(PlaceBody(rv, [PLine(P.Pol3.PurchaseOrderLineId, 10m, 1_000m)]), 400, 404);
    }

    [Fact]
    public async Task IT_B5_065_Place_Missing_Line_Is_Rejected_Or_Keeps_It()
    {
        var id = P.Po1.PurchaseOrderId;
        var rv = await RvAsync(id);
        var before = await Snap5();

        var r = await Call("manager", Post, PlaceUrl(id), PlaceBody(rv, [PLine(P.Pol1.PurchaseOrderLineId, 110m, 49_500m)]));

        r.ShouldNotBeServerError();
        if (r.Code == 200) (await PolsAsync(id)).Should().HaveCount(2);
        else { r.Code.Should().Be(400); await Unchanged(before); }
    }

    [Theory]
    [InlineData("IT_B5_066", "unknown")]
    [InlineData("IT_B5_066", "twice")]
    public async Task IT_B5_066_Place_With_Unknown_Or_Duplicate_Line(string caseId, string kind)
    {
        var rv = await RvAsync(P.Po1.PurchaseOrderId);
        object[] lines = kind == "unknown"
            ? [PLine(P.Pol1.PurchaseOrderLineId, 1m, 1m), PLine(P.Pol2.PurchaseOrderLineId, 1m, 1m), PLine(P.Pol2.PurchaseOrderLineId + 1_000_000, 1m, 1m)]
            : [PLine(P.Pol1.PurchaseOrderLineId, 1m, 1m), PLine(P.Pol1.PurchaseOrderLineId, 2m, 2m), PLine(P.Pol2.PurchaseOrderLineId, 1m, 1m)];

        await RejectPlace(PlaceBody(rv, lines), 400, 404);
    }

    [Theory]
    [InlineData("IT_B5_067", "qty0")]
    [InlineData("IT_B5_067", "qtyNeg")]
    [InlineData("IT_B5_067", "qtyHuge")]
    [InlineData("IT_B5_067", "priceNeg")]
    [InlineData("IT_B5_067", "priceHuge")]
    public async Task IT_B5_067_Place_Invalid_Numbers_Roll_Back_Everything(string caseId, string kind)
    {
        var rv = await RvAsync(P.Po1.PurchaseOrderId);
        var (q, p) = kind switch
        {
            "qty0" => (0m, 1m), "qtyNeg" => (-1m, 1m), "qtyHuge" => (Dc("1000000000000000"), 1m),
            "priceNeg" => (1m, -1m), _ => (1m, Dc("10000000000000000")),
        };
        object[] lines = [PLine(P.Pol1.PurchaseOrderLineId, q, p), PLine(P.Pol2.PurchaseOrderLineId, 5m, 5m)];

        await RejectPlace(PlaceBody(rv, lines, B.Su2.SupplierId, note: "Đổi ghi chú"), 400);
    }

    [Theory]
    [InlineData("IT_B5_068", "discontinued")]
    [InlineData("IT_B5_068", "missing")]
    public async Task IT_B5_068_Place_With_Invalid_Supplier(string caseId, string kind)
    {
        var rv = await RvAsync(P.Po1.PurchaseOrderId);
        var sid = kind == "discontinued" ? B.Su3.SupplierId : await MissingSupplierIdAsync();

        await RejectPlace(PlaceBody(rv, Po1Lines(), sid), 400, 404);
        (await PoAsync(P.Po1.PurchaseOrderId)).SupplierId.Should().Be(B.Su1.SupplierId);
    }

    [Theory]
    [InlineData("IT_B5_069", "orderDateZero")]
    [InlineData("IT_B5_069", "datesZero")]
    [InlineData("IT_B5_069", "countMismatch")]
    public async Task IT_B5_069_Place_Invalid_Dates_Keep_Old_Schedule(string caseId, string kind)
    {
        var rv = await RvAsync(P.Po1.PurchaseOrderId);
        var body = kind switch
        {
            "datesZero" => PlaceBody(rv, Po1Lines(), dates: [new DateOnly(1, 1, 1), T.AddDays(7)]),
            "countMismatch" => PlaceBody(rv, Po1Lines(), count: 2, dates: Dates(3)),
            _ => PlaceBody(rv, Po1Lines()),
        };
        if (kind == "orderDateZero") body["OrderDate"] = "0001-01-01";

        await RejectPlace(body, 400);
        (await PoDsdAsync(P.Po1.PurchaseOrderId)).Should().HaveCount(2);
    }

    [Theory]
    [InlineData("IT_B5_070", "ordered")]
    [InlineData("IT_B5_070", "cancelled")]
    [InlineData("IT_B5_070", "partial")]
    public async Task IT_B5_070_Place_Only_Allowed_For_Pending_Finalisation(string caseId, string kind)
    {
        var po = kind switch { "ordered" => P.Po2, "cancelled" => P.Po4, _ => P.Po5 };
        var lines = (await PolsAsync(po.PurchaseOrderId)).Select(l => PLine(l.PurchaseOrderLineId, l.OrderedQty, l.UnitPrice)).ToArray();
        var before = await Snap5();

        var r = await Call("manager", Post, PlaceUrl(po.PurchaseOrderId), PlaceBody(await RvAsync(po.PurchaseOrderId), lines, count: 1));

        Rejected(r, 400, 409);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_071", "missing")]
    [InlineData("IT_B5_071", "abc")]
    public async Task IT_B5_071_Place_Unknown_Id_Returns_404(string caseId, string kind)
    {
        var before = await Snap5();
        var id = kind == "abc" ? "abc" : (await MissingPoIdAsync()).ToString();

        var r = await Call("manager", Post, $"{TRK}/{id}/place", PlaceBody(null, Po1Lines()));

        r.Code.Should().Be(404, r.ToString());
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_073_Place_Note_Too_Long_Is_Rejected()
    {
        var rv = await RvAsync(P.Po1.PurchaseOrderId);

        await RejectPlace(PlaceBody(rv, Po1Lines(), note: new string('ộ', 1001)), 400);
    }

    // ------------------------------------------------------------------ cancel

    [Fact]
    public async Task IT_B5_074_Cancel_Records_Reason_And_Keeps_Children()
    {
        var id = P.Po1.PurchaseOrderId;
        var rv = await RvAsync(id);
        var audit = await AuditCountAsync5();

        var r = await Call("manager", Post, CancelUrl(id), Reason(rv, "NCC hết hàng"));

        r.Code.Should().Be(200, r.ToString());
        var po = await PoAsync(id);
        po.GoodsStatus.Should().BeOneOf("CANCELLED", "REJECTED");
        po.RejectReason.Should().Be("NCC hết hàng");
        po.RowVer.Should().NotEqual(rv);
        (await PolsAsync(id)).Should().HaveCount(2);
        (await PoDsdAsync(id)).Should().HaveCount(2);
        (await AuditCountAsync5()).Should().Be(audit + 1);
    }

    [Theory]
    [InlineData("IT_B5_075", "")]
    [InlineData("IT_B5_075", "null")]
    [InlineData("IT_B5_075", "missing")]
    public async Task IT_B5_075_Cancel_Requires_Reason(string caseId, string kind)
    {
        var body = Reason(await RvAsync(P.Po1.PurchaseOrderId), kind == "" ? "" : null);
        if (kind == "missing") body.Remove("Reason");
        var before = await Snap5();

        var r = await Call("manager", Post, CancelUrl(P.Po1.PurchaseOrderId), body);

        Rejected(r, 400);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_076", 500, 200)]
    [InlineData("IT_B5_076", 501, 400)]
    public async Task IT_B5_076_Cancel_Reason_Length(string caseId, int len, int expected)
    {
        var reason = new string('ạ', len);
        var before = await Snap5();

        var r = await Call("manager", Post, CancelUrl(P.Po1.PurchaseOrderId), Reason(await RvAsync(P.Po1.PurchaseOrderId), reason));

        r.ShouldNotBeServerError();
        r.Code.Should().Be(expected, r.ToString());
        if (expected == 200) (await PoAsync(P.Po1.PurchaseOrderId)).RejectReason.Should().Be(reason);
        else await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_077", "received")]
    [InlineData("IT_B5_077", "paid")]
    public async Task IT_B5_077_Cancel_Blocked_After_Receipt_Or_Payment(string caseId, string kind)
    {
        var po = kind == "received" ? P.Po5 : P.Po3;
        var before = await Snap5();

        var r = await Call("manager", Post, CancelUrl(po.PurchaseOrderId), Reason(await RvAsync(po.PurchaseOrderId), "Hủy"));

        Rejected(r, 400, 409);
        await Unchanged(before);
        await using var db = NewDb();
        (await db.MaterialReceiptLines.AsNoTracking().AnyAsync(l => l.PurchaseOrderLineId == P.Pol5.PurchaseOrderLineId)).Should().BeTrue();
        (await PaysAsync(P.Po3.PurchaseOrderId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task IT_B5_078_Cancel_Already_Cancelled_Keeps_Old_Reason()
    {
        var before = await Snap5();

        var r = await Call("manager", Post, CancelUrl(P.Po4.PurchaseOrderId), Reason(await RvAsync(P.Po4.PurchaseOrderId), "Lần hai"));

        Rejected(r, 400, 409);
        (await PoAsync(P.Po4.PurchaseOrderId)).RejectReason.Should().Be("NCC hết hàng");
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_079_Cancel_Stale_RowVer_Or_Missing_Id()
    {
        var stale = await RvAsync(P.Po1.PurchaseOrderId);
        await BumpAsync(P.Po1.PurchaseOrderId);
        var before = await Snap5();

        var a = await Call("manager", Post, CancelUrl(P.Po1.PurchaseOrderId), Reason(stale, "Hủy"));
        var b = await Call("manager", Post, CancelUrl(await MissingPoIdAsync()), Reason(stale, "Hủy"));

        a.Code.Should().Be(409, a.ToString());
        b.Code.Should().Be(404, b.ToString());
        await Unchanged(before);
    }

    // ------------------------------------------------------------------ reprice

    [Fact]
    public async Task IT_B5_081_Reprice_Changes_Only_UnitPrice()
    {
        var id = P.Po2.PurchaseOrderId;
        var rv = await RvAsync(id);
        var audit = await AuditCountAsync5();

        var r = await Call("manager", Post, RepriceUrl(id), Rep(rv, [RLine(P.Pol3.PurchaseOrderLineId, 46_000m)]));

        r.Code.Should().Be(200, r.ToString());
        var l = await PolAsync(P.Pol3.PurchaseOrderLineId);
        (l.UnitPrice, l.Amount, l.OrderedQty).Should().Be((46_000m, 9_200_000.00m, 200m));
        var po = await PoAsync(id);
        po.Note.Should().Be("NCC giảm giá");
        po.RowVer.Should().NotEqual(rv);
        (await AuditCountAsync5()).Should().Be(audit + 1);
    }

    [Fact]
    public async Task IT_B5_082_Reprice_Paid_Po_Keeps_Payment_Status_Consistent()
    {
        var id = P.Po3.PurchaseOrderId;
        var before = await Snap5();

        var r = await Call("manager", Post, RepriceUrl(id), Rep(await RvAsync(id), [RLine(P.Pol31.PurchaseOrderLineId, 90_000m)]));

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(200, 409);
        if (r.Code == 200)
        {
            var po = await PoAsync(id);
            var total = (await PolsAsync(id)).Sum(l => l.Amount);
            po.PaymentStatus.Should().Be(po.PaidAmount >= total ? "PAID" : "PARTIALLY_PAID");
            (await PaysAsync(id)).Select(p => p.Amount).Should().Equal(5_000_000m, 3_000_000m);
        }
        else await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_083_Reprice_With_Line_Of_Another_Po_Is_Rejected()
    {
        var before = await Snap5();

        var r = await Call("manager", Post, RepriceUrl(P.Po2.PurchaseOrderId), Rep(await RvAsync(P.Po2.PurchaseOrderId), [RLine(P.Pol1.PurchaseOrderLineId, 1m)]));

        Rejected(r, 400, 404);
        await Unchanged(before);
    }

    [Theory]
    [InlineData("IT_B5_084", "priceNeg")]
    [InlineData("IT_B5_084", "priceHuge")]
    [InlineData("IT_B5_084", "price3dec")]
    [InlineData("IT_B5_084", "noLines")]
    public async Task IT_B5_084_Reprice_Invalid_Values(string caseId, string kind)
    {
        var rv = await RvAsync(P.Po2.PurchaseOrderId);
        object[] lines = kind switch
        {
            "priceNeg" => [RLine(P.Pol3.PurchaseOrderLineId, -1m)],
            "priceHuge" => [RLine(P.Pol3.PurchaseOrderLineId, Dc("10000000000000000"))],
            "price3dec" => [RLine(P.Pol3.PurchaseOrderLineId, 100.555m)],
            _ => [],
        };
        var before = await Snap5();

        var r = await Call("manager", Post, RepriceUrl(P.Po2.PurchaseOrderId), Rep(rv, lines));

        r.ShouldNotBeServerError();
        r.Body.Should().NotContainAny(Leaks);
        if (kind is "price3dec" or "noLines") r.Code.Should().BeOneOf(200, 400);
        else r.Code.Should().Be(400, r.ToString());
        if (r.Code == 400) await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_085_Reprice_Duplicate_Line_Is_Rejected()
    {
        var before = await Snap5();
        var rv = await RvAsync(P.Po2.PurchaseOrderId);

        var r = await Call("manager", Post, RepriceUrl(P.Po2.PurchaseOrderId),
            Rep(rv, [RLine(P.Pol3.PurchaseOrderLineId, 40_000m), RLine(P.Pol3.PurchaseOrderLineId, 41_000m)]));

        Rejected(r, 400);
        await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_086_Reprice_Pending_Or_Cancelled_Po()
    {
        var before = await Snap5();

        var pending = await Call("manager", Post, RepriceUrl(P.Po1.PurchaseOrderId), Rep(await RvAsync(P.Po1.PurchaseOrderId), [RLine(P.Pol1.PurchaseOrderLineId, 1m)]));
        var cancelled = await Call("manager", Post, RepriceUrl(P.Po4.PurchaseOrderId), Rep(await RvAsync(P.Po4.PurchaseOrderId), []));

        pending.ShouldNotBeServerError();
        pending.Code.Should().BeOneOf(200, 400, 409);
        Rejected(cancelled, 400, 409);
        (await PoAsync(P.Po4.PurchaseOrderId)).GoodsStatus.Should().Be("REJECTED");
        if (pending.Code != 200) await Unchanged(before);
    }

    [Fact]
    public async Task IT_B5_087_Reprice_Stale_Or_Missing_RowVer()
    {
        var stale = await RvAsync(P.Po2.PurchaseOrderId);
        await BumpAsync(P.Po2.PurchaseOrderId);
        var before = await Snap5();
        var line = new object[] { RLine(P.Pol3.PurchaseOrderLineId, 40_000m) };
        var noRv = Rep(null, line);
        noRv.Remove("RowVer");

        var a = await Call("manager", Post, RepriceUrl(P.Po2.PurchaseOrderId), Rep(stale, line));
        var b = await Call("manager", Post, RepriceUrl(P.Po2.PurchaseOrderId), noRv);

        a.Code.Should().Be(409, a.ToString());
        Rejected(b, 400, 409);
        await Unchanged(before);
    }
}
