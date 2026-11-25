using FluentAssertions;
using Integration.Tests.Batch1;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>IT_B6_034–042 — PUT WK/MaterialReceipts/{id}.</summary>
public sealed class B6UpdateReceiptTests(ITestOutputHelper output) : B6TestBase(output)
{
    private Task<Api> Put6(int id, object body, string who = "warehouse") => Call(who, Put, $"{MRW}/{id}", body);

    [Fact]
    public async Task IT_B6_034_Update_Pending_Receipt_Changes_Lines_And_RowVer_Not_Stock()
    {
        var id = R.Mr1.MaterialReceiptId;
        var rvBefore = (await MrAsync(id))!.RowVer;
        var stockBefore = await OnHandAsync(B.Ma2.MaterialId);
        var stMax = await MaxStAsync();

        var r = await Put6(id, CustBody([CL(R.Om1.OrderMaterialId, 120m), CL(R.Om2.OrderMaterialId, 100m)], "Sửa SL"));

        r.Code.Should().Be(200, r.ToString());
        var mr = (await MrAsync(id))!;
        mr.Status.Should().Be(ReceiptStatuses.Pending);
        mr.Note.Should().Be("Sửa SL");
        mr.RowVer.Should().NotEqual(rvBefore);
        var lines = await MrlAsync(id);
        lines.Should().HaveCount(2);
        lines.Single(l => l.OrderMaterialId == R.Om1.OrderMaterialId).AcceptedQty.Should().Be(120m);
        lines.Single(l => l.OrderMaterialId == R.Om2.OrderMaterialId).AcceptedQty.Should().Be(100m);
        (await OnHandAsync(B.Ma2.MaterialId)).Should().Be(stockBefore);
        (await MaxStAsync()).Should().Be(stMax);
    }

    [Fact]
    public async Task IT_B6_035_Update_Can_Remove_A_Line()
    {
        var id = R.Mr1.MaterialReceiptId;

        var r = await Put6(id, CustBody([CL(R.Om2.OrderMaterialId, 100m)]));

        r.Code.Should().Be(200, r.ToString());
        var lines = await MrlAsync(id);
        lines.Should().ContainSingle().Which.OrderMaterialId.Should().Be(R.Om2.OrderMaterialId);
        lines.Should().NotContain(l => l.MaterialReceiptLineId == R.Mrl1.MaterialReceiptLineId);
    }

    [Fact]
    public async Task IT_B6_036_Update_Confirmed_Receipt_Returns_409()
    {
        var before = await Snap6();

        var r = await Put6(R.Mr3.MaterialReceiptId, SupBody([SL(R.Pol1.PurchaseOrderLineId, 10m)], poId: R.Po1.PurchaseOrderId));

        Graceful(r, 409);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_037_Update_Returned_Receipt_Resubmits_As_Pending()
    {
        var id = R.Mr4.MaterialReceiptId;

        var r = await Put6(id, CustBody([CL(R.Om1.OrderMaterialId, 10m)]));

        r.Code.Should().Be(200, r.ToString());
        var mr = (await MrAsync(id))!;
        mr.Status.Should().Be(ReceiptStatuses.Pending);
        Note($"ReturnReason sau nộp lại = '{mr.ReturnReason}', SubmittedAt = {mr.SubmittedAt:O}");
    }

    [Fact]
    public async Task IT_B6_038_Switching_Source_Is_Consistent_With_Computed_Column()
    {
        var id = R.Mr1.MaterialReceiptId;
        var body = SupBody([SL(R.Pol3.PurchaseOrderLineId, 10m)]);

        var r = await Put6(id, body);

        Graceful(r, 200, 400);
        if (r.Code == 200)
        {
            var mr = (await MrAsync(id))!;
            mr.Source.Should().Be(MaterialSources.Supplier);
            (await MrlAsync(id)).Should().OnlyContain(l => l.OrderMaterialId == null);
        }
        else (await MrAsync(id))!.Source.Should().Be(MaterialSources.Customer);
    }

    [Fact]
    public async Task IT_B6_040_Other_Warehouse_Keeper_Updating_Receipt_Is_Consistent()
    {
        var id = R.Mr1.MaterialReceiptId;
        var token = await TokenAsync(R.Wh2);

        var r = await SendAsync(Put, $"{MRW}/{id}", token, CustBody([CL(R.Om1.OrderMaterialId, 77m)]));

        Graceful(r, 200, 403, 404);
        var qty = (await MrlAsync(id)).Single(l => l.OrderMaterialId == R.Om1.OrderMaterialId).AcceptedQty;
        qty.Should().Be(r.Code == 200 ? 77m : 100m, "[DB] phải nhất quán với API");
        Note($"TK_WK2 PUT phiếu của người khác -> {r.Code}");
    }

    [Theory]
    [InlineData("IT_B6_041", "missing")]
    [InlineData("IT_B6_041", "abc")]
    public async Task IT_B6_041_Unknown_Receipt_Returns_404_And_No_Upsert(string caseId, string kind)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var id = kind == "abc" ? "abc" : (await MissingMrIdAsync()).ToString();

        var r = await Call("warehouse", Put, $"{MRW}/{id}", CustBody());

        Graceful(r, 404);
        (await MaxMrIdAsync()).Should().Be(maxId);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_042", "duplicate")]
    [InlineData("IT_B6_042", "negative")]
    public async Task IT_B6_042_Failed_Update_Rolls_Back_Existing_Lines(string caseId, string kind)
    {
        var before = await Snap6();
        var lines = kind == "duplicate"
            ? new[] { CL(R.Om1.OrderMaterialId, 10m), CL(R.Om1.OrderMaterialId, 20m) }
            : new[] { CL(R.Om1.OrderMaterialId, -1m) };

        var r = await Put6(R.Mr1.MaterialReceiptId, CustBody(lines));

        if (kind == "duplicate") Graceful(r, 400, 409); else Graceful(r, 400);
        (await MrlAsync(R.Mr1.MaterialReceiptId)).Should().ContainSingle().Which.AcceptedQty.Should().Be(100m);
        await Unchanged6(before);
    }
}
