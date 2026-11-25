using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>IT_B6_013–030 — POST WK/MaterialReceipts.</summary>
public sealed class B6CreateReceiptTests(ITestOutputHelper output) : B6TestBase(output)
{
    private async Task<Api> Post6(object body, string who = "warehouse") => await Call(who, Post, MRW, body);

    private async Task NoNewReceipt(int maxBefore, Snap6 before)
    {
        (await MaxMrIdAsync()).Should().Be(maxBefore, "[DB] không được có phiếu mới");
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_013_Create_Customer_Receipt_Is_Pending_And_Does_Not_Touch_Stock()
    {
        var maxId = await MaxMrIdAsync();
        var stockBefore = await OnHandAsync(B.Ma2.MaterialId);
        var stMax = await MaxStAsync();

        var r = await Post6(CustBody(note: "Khách giao đợt 2"));

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(r.Data, "receiptCode").Should().NotBeNullOrWhiteSpace();
        B1Json.Str(r.Data, "receiptType")!.Should().BeOneOf("NEW", "New", "Mới");
        var mr = await NewestMrAsync(maxId);
        mr.Status.Should().Be(ReceiptStatuses.Pending);
        mr.Source.Should().Be(MaterialSources.Customer);
        mr.OrderId.Should().Be(R.Od6.OrderId);
        mr.ShortageRequestId.Should().BeNull();
        mr.ConfirmedBy.Should().BeNull();
        NearVnNow(mr.SubmittedAt, "SubmittedAt");
        var lines = await MrlAsync(mr.MaterialReceiptId);
        lines.Should().HaveCount(2);
        lines.Select(l => l.OrderMaterialId).Should().BeEquivalentTo([R.Om1.OrderMaterialId, R.Om2.OrderMaterialId]);
        (await OnHandAsync(B.Ma2.MaterialId)).Should().Be(stockBefore);
        (await MaxStAsync()).Should().Be(stMax, "chưa xác nhận thì chưa có thẻ kho");
    }

    [Fact]
    public async Task IT_B6_014_Create_Supplier_Receipt_For_Reserve_Po()
    {
        var maxId = await MaxMrIdAsync();

        var r = await Post6(SupBody([SL(R.Pol3.PurchaseOrderLineId, 200m)]));

        r.Code.Should().Be(200, r.ToString());
        var mr = await NewestMrAsync(maxId);
        mr.Source.Should().Be(MaterialSources.Supplier);
        mr.PurchaseOrderId.Should().Be(R.Po2.PurchaseOrderId);
        var line = (await MrlAsync(mr.MaterialReceiptId)).Single();
        line.PurchaseOrderLineId.Should().Be(R.Pol3.PurchaseOrderLineId);
        line.OrderMaterialId.Should().BeNull();
        line.MaterialId.Should().Be(B.Ma1.MaterialId);
    }

    [Fact]
    public async Task IT_B6_015_Customer_Source_With_PurchaseOrder_Is_Rejected()
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var body = CustBody();
        body["PurchaseOrderId"] = R.Po2.PurchaseOrderId;

        var r = await Post6(body);

        Graceful(r, 400);
        await NoNewReceipt(maxId, before);
    }

    [Fact]
    public async Task IT_B6_016_Missing_Order_Or_PurchaseOrder_For_Source_Is_Rejected()
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var cust = CustBody(); cust["OrderId"] = null;
        var sup = SupBody(); sup["PurchaseOrderId"] = null;

        Graceful(await Post6(cust), 400);
        Graceful(await Post6(sup), 400);

        await NoNewReceipt(maxId, before);
    }

    [Theory]
    [InlineData("IT_B6_017", "Customer")]
    [InlineData("IT_B6_017", "OTHER")]
    [InlineData("IT_B6_017", "")]
    [InlineData("IT_B6_017", "ABCDEFGHIJK")]
    public async Task IT_B6_017_Invalid_Source_Value(string caseId, string source)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var body = CustBody(); body["Source"] = source;

        var r = await Post6(body);

        if (source == "Customer")
        {
            Graceful(r, 200, 400);
            if (r.Code == 200) (await NewestMrAsync(maxId)).Source.Should().Be(MaterialSources.Customer);
            else await NoNewReceipt(maxId, before);
        }
        else
        {
            Graceful(r, 400);
            await NoNewReceipt(maxId, before);
        }
    }

    [Fact]
    public async Task IT_B6_018_Line_From_Another_Order_Is_Rejected_And_Rolled_Back()
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();

        var r = await Post6(CustBody([CL(R.Om1.OrderMaterialId, 10m), CL(R.Om9.OrderMaterialId, 10m)]));

        Graceful(r, 400, 404);
        await NoNewReceipt(maxId, before);
    }

    [Fact]
    public async Task IT_B6_019_Line_From_Another_PurchaseOrder_Is_Rejected()
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();

        var r = await Post6(SupBody([SL(R.Pol3.PurchaseOrderLineId, 10m), SL(R.Pol1.PurchaseOrderLineId, 10m)]));

        Graceful(r, 400, 404);
        await NoNewReceipt(maxId, before);
    }

    [Theory]
    [InlineData("IT_B6_020", "po-line-on-customer")]
    [InlineData("IT_B6_020", "both")]
    [InlineData("IT_B6_020", "neither")]
    public async Task IT_B6_020_Line_Must_Reference_Exactly_The_Source_Key(string caseId, string kind)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        object line = kind switch
        {
            "po-line-on-customer" => RawLine(null, R.Pol3.PurchaseOrderLineId, 10m),
            "both" => RawLine(R.Om1.OrderMaterialId, R.Pol3.PurchaseOrderLineId, 10m),
            _ => RawLine(null, null, 10m),
        };

        var r = await Post6(CustBody([line]));

        Graceful(r, 400);
        await NoNewReceipt(maxId, before);
    }

    [Fact]
    public async Task IT_B6_021_Duplicate_OrderMaterial_In_One_Receipt_Is_Rejected_Not_500()
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();

        var r = await Post6(CustBody([CL(R.Om1.OrderMaterialId, 10m), CL(R.Om1.OrderMaterialId, 20m)]));

        Graceful(r, 400, 409);
        await NoNewReceipt(maxId, before);
    }

    [Fact]
    public async Task IT_B6_022_Duplicate_Stock_Material_In_One_Receipt_Is_Rejected_Not_500()
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();

        var r = await Post6(SupBody([SL(R.Pol3.PurchaseOrderLineId, 10m), SL(R.Pol3.PurchaseOrderLineId, 20m)]));

        Graceful(r, 400, 409);
        await NoNewReceipt(maxId, before);
    }

    [Theory]
    [InlineData("IT_B6_023", "0")]
    [InlineData("IT_B6_023", "-0.001")]
    [InlineData("IT_B6_023", "0.001")]
    [InlineData("IT_B6_023", "999999999999999.999")]
    [InlineData("IT_B6_023", "1000000000000000")]
    [InlineData("IT_B6_023", "1.0005")]
    public async Task IT_B6_023_AcceptedQty_Boundaries(string caseId, string raw)
    {
        var qty = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();

        var r = await Post6(CustBody([CL(R.Om1.OrderMaterialId, qty)]));

        var expected = raw switch
        {
            "0" or "-0.001" or "1000000000000000" => new[] { 400 },
            "0.001" => new[] { 200 },
            _ => new[] { 200, 400 },
        };
        Graceful(r, expected);
        if (r.Code == 200)
        {
            var lines = await MrlAsync((await NewestMrAsync(maxId)).MaterialReceiptId);
            lines.Should().OnlyContain(l => l.AcceptedQty > 0m);
        }
        else await NoNewReceipt(maxId, before);
    }

    [Fact]
    public async Task IT_B6_024_Quantity_Above_Outstanding_Is_Handled_Consistently()
    {
        var maxId = await MaxMrIdAsync();

        var r = await Post6(CustBody([CL(R.Om1.OrderMaterialId, 200m)]));

        Graceful(r, 200, 400, 409);
        var created = (await MaxMrIdAsync()) > maxId;
        created.Should().Be(r.Code == 200, "[DB] phải nhất quán với API");
        Note($"qty 200 > khách cấp 150 -> {r.Code}");
    }

    [Theory]
    [InlineData("IT_B6_025", "min")]
    [InlineData("IT_B6_025", "missing")]
    [InlineData("IT_B6_025", "tomorrow")]
    public async Task IT_B6_025_ReceiptDate_Boundaries(string caseId, string kind)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var body = CustBody();
        if (kind == "min") body["ReceiptDate"] = DateOnly.MinValue;
        else if (kind == "missing") body.Remove("ReceiptDate");
        else body["ReceiptDate"] = T.AddDays(1);

        var r = await Post6(body);

        if (kind == "tomorrow") Graceful(r, 200, 400); else Graceful(r, 400);
        if (r.Code != 200) await NoNewReceipt(maxId, before);
        (await AllMrAsync()).Should().OnlyContain(m => m.ReceiptDate != DateOnly.MinValue);
    }

    [Theory]
    [InlineData("IT_B6_026", 1000)]
    [InlineData("IT_B6_026", 1001)]
    public async Task IT_B6_026_Note_Length_Boundary(string caseId, int length)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var note = Vn(length);

        var r = await Post6(CustBody(note: note));

        if (length == 1000)
        {
            r.Code.Should().Be(200, r.ToString());
            (await NewestMrAsync(maxId)).Note.Should().Be(note);
        }
        else
        {
            Graceful(r, 400);
            await NoNewReceipt(maxId, before);
        }
    }

    [Theory]
    [InlineData("IT_B6_027", "empty")]
    [InlineData("IT_B6_027", "null")]
    public async Task IT_B6_027_Empty_Or_Null_Lines_Are_Rejected(string caseId, string kind)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var body = CustBody(); body["Lines"] = kind == "empty" ? Array.Empty<object>() : null;

        var r = await Post6(body);

        Graceful(r, 400);
        await NoNewReceipt(maxId, before);
    }

    [Theory]
    [InlineData("IT_B6_028", "order-missing")]
    [InlineData("IT_B6_028", "order-cancelled")]
    [InlineData("IT_B6_028", "po-missing")]
    [InlineData("IT_B6_028", "po-rejected")]
    public async Task IT_B6_028_Unknown_Or_Cancelled_Parent_Is_Rejected(string caseId, string kind)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var missingOrder = (await MissingOrderLineIdAsync()) + 1;
        var body = kind switch
        {
            "order-missing" => CustBody(orderId: missingOrder),
            "order-cancelled" => CustBody(orderId: D.Od3.OrderId),
            "po-missing" => SupBody(poId: await MissingPoIdAsync()),
            _ => SupBody([SL(R.Pol4.PurchaseOrderLineId, 10m)], poId: R.Po4.PurchaseOrderId),
        };

        var r = await Post6(body);

        Graceful(r, 400, 404);
        await NoNewReceipt(maxId, before);
    }

    [Theory]
    [InlineData("IT_B6_029", "om")]
    [InlineData("IT_B6_029", "pol")]
    public async Task IT_B6_029_Unknown_Line_Key_Is_Rejected_And_Rolled_Back(string caseId, string kind)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var body = kind == "om"
            ? CustBody([CL(R.Om1.OrderMaterialId, 10m), CL(await MissingOmIdAsync(), 10m)])
            : SupBody([SL(R.Pol3.PurchaseOrderLineId, 10m), SL(await MissingPolIdAsync(), 10m)]);

        var r = await Post6(body);

        Graceful(r, 400, 404);
        await NoNewReceipt(maxId, before);
    }

    [Fact]
    public async Task IT_B6_030_Mass_Assignment_Fields_Are_Ignored()
    {
        var maxId = await MaxMrIdAsync();
        var body = CustBody();
        body["ReceiptCode"] = "HACK";
        body["Status"] = "CONFIRMED";
        body["ConfirmedBy"] = S0.Adm.EmployeeId;
        body["ConfirmedAt"] = "2026-01-01T00:00:00";
        body["SubmittedAt"] = "0001-01-01T00:00:00";

        var r = await Post6(body);

        r.Code.Should().Be(200, r.ToString());
        var mr = await NewestMrAsync(maxId);
        mr.ReceiptCode.Should().NotBe("HACK");
        mr.Status.Should().Be(ReceiptStatuses.Pending);
        mr.ConfirmedBy.Should().BeNull();
        mr.ConfirmedAt.Should().BeNull();
        mr.Source.Should().Be(MaterialSources.Customer);
        NearVnNow(mr.SubmittedAt, "SubmittedAt");
    }
}
