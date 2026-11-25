using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>IT_B6_044–061 — M/MaterialReceipts/{id}/confirm, /return và GET chi tiết.</summary>
public sealed class B6ConfirmReturnTests(ITestOutputHelper output) : B6TestBase(output)
{
    [Fact]
    public async Task IT_B6_044_Confirm_Customer_Receipt_Posts_Stock_And_Sets_Confirmer()
    {
        var id = R.Mr1.MaterialReceiptId;
        var rv = (await MrAsync(id))!.RowVer;
        var onHand = await OnHandAsync(B.Ma2.MaterialId);

        var r = await Confirm(id);

        r.Code.Should().Be(200, r.ToString());
        var mr = (await MrAsync(id))!;
        mr.Status.Should().Be(ReceiptStatuses.Confirmed);
        mr.ConfirmedBy.Should().Be(S0.Mgr.EmployeeId);
        NearVnNow(mr.ConfirmedAt, "ConfirmedAt");
        mr.RowVer.Should().NotEqual(rv);
        var sb = (await SbAsync(B.Ma2.MaterialId))!;
        sb.OnHandQty.Should().Be(onHand + 100m);
        NearVnNow(sb.UpdatedAt, "StockBalance.UpdatedAt");
        var st = (await StAsync(id)).Should().ContainSingle().Subject;
        st.SourceDocType.Should().Be(StockDocTypes.MaterialReceipt);
        st.SourceLineId.Should().Be(R.Mrl1.MaterialReceiptLineId);
        st.QtyChange.Should().Be(100m);
        st.BalanceAfter.Should().Be(sb.OnHandQty);
        Note($"TransactionDate = {st.TransactionDate}, ReceiptDate = {mr.ReceiptDate}");
    }

    [Fact]
    public async Task IT_B6_045_Confirm_Short_Supplier_Receipt_Creates_ShortDelivery_Request()
    {
        var id = R.Mr2.MaterialReceiptId;
        var onHand = await OnHandAsync(B.Ma1.MaterialId);

        var r = await Confirm(id);

        r.Code.Should().Be(200, r.ToString());
        B1Json.Int(r.Data, "shortDeliveryRequestId").Should().NotBeNull();
        (await OnHandAsync(B.Ma1.MaterialId)).Should().Be(onHand + 150m);
        (await StAsync(id)).Should().ContainSingle().Which.QtyChange.Should().Be(150m);
        var req = (await ShortOfReceiptAsync(id)).Should().ContainSingle().Subject;
        req.RequestCode.Should().NotBeNullOrWhiteSpace();
        Note($"SHORT_DELIVERY Status = {req.Status}");
        var line = (await RlAsync(req.RequestId)).Should().ContainSingle().Subject;
        line.Quantity.Should().Be(50m);
        line.MaterialReceiptLineId.Should().Be(R.Mrl2.MaterialReceiptLineId);
        line.MaterialId.Should().Be(B.Ma1.MaterialId);
    }

    [Fact]
    public async Task IT_B6_046_Second_Confirm_Returns_409_And_Does_Not_Double_Post()
    {
        var id = R.Mr1.MaterialReceiptId;
        (await Confirm(id)).Code.Should().Be(200);
        var onHand = await OnHandAsync(B.Ma2.MaterialId);
        var stMax = await MaxStAsync();
        var before = await Snap6();

        var r = await Confirm(id);

        Graceful(r, 409);
        (await OnHandAsync(B.Ma2.MaterialId)).Should().Be(onHand);
        (await MaxStAsync()).Should().Be(stMax);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_051_Confirm_Adds_To_Existing_StockBalance_Without_Second_Row()
    {
        var onHand = await OnHandAsync(B.Ma1.MaterialId);
        (await Confirm(R.Mr2.MaterialReceiptId)).Code.Should().Be(200);
        var second = await Call("warehouse", Post, MRW, SupBody([SL(R.Pol3.PurchaseOrderLineId, 50m)]));
        second.Code.Should().Be(200, second.ToString());
        await SeedUploadAsync(AttachmentOwnerTypes.MaterialReceipt, ReceiptIdOf(second), "warehouse", "DOCUMENT_PHOTO");

        var r = await Confirm(ReceiptIdOf(second));

        r.Code.Should().Be(200, r.ToString());
        await using var db = NewDb();
        var rows = await db.StockBalances.AsNoTracking().Where(x => x.WarehouseType == WarehouseTypes.Material && x.MaterialId == B.Ma1.MaterialId).ToListAsync();
        rows.Should().ContainSingle("UQ_SB_Item: không INSERT dòng tồn thứ hai");
        rows[0].OnHandQty.Should().Be(onHand + 150m + 50m);
    }

    [Fact]
    public async Task IT_B6_052_Admin_Confirm_Uses_Admin_EmployeeId_From_Token()
    {
        var r = await Confirm(R.Mr1.MaterialReceiptId, "admin");

        r.Code.Should().Be(200, r.ToString());
        (await MrAsync(R.Mr1.MaterialReceiptId))!.ConfirmedBy.Should().Be(S0.Adm.EmployeeId);
    }

    [Fact]
    public async Task IT_B6_053_Confirm_Returned_Receipt_Is_Rejected()
    {
        var before = await Snap6();

        var r = await Confirm(R.Mr4.MaterialReceiptId);

        Graceful(r, 400, 409);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_054", "missing")]
    [InlineData("IT_B6_054", "abc")]
    public async Task IT_B6_054_Confirm_Unknown_Receipt_Returns_404(string caseId, string kind)
    {
        var before = await Snap6();
        var id = kind == "abc" ? "abc" : (await MissingMrIdAsync()).ToString();

        var r = await Call("manager", Post, $"{MRM}/{id}/confirm");

        Graceful(r, 404);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_055_Return_Receipt_Stores_Reason_Without_Touching_Stock()
    {
        var id = R.Mr1.MaterialReceiptId;
        var rv = (await MrAsync(id))!.RowVer;
        var stMax = await MaxStAsync();
        var onHand = await OnHandAsync(B.Ma2.MaterialId);

        var r = await ReturnMr(id, new { Reason = "Số lượng không khớp thực tế" });

        r.Code.Should().Be(200, r.ToString());
        var mr = (await MrAsync(id))!;
        mr.Status.Should().Be(ReceiptStatuses.Returned);
        mr.ReturnReason.Should().Be("Số lượng không khớp thực tế");
        mr.RowVer.Should().NotEqual(rv);
        (await MaxStAsync()).Should().Be(stMax);
        (await OnHandAsync(B.Ma2.MaterialId)).Should().Be(onHand);
    }

    [Theory]
    [InlineData("IT_B6_056", "empty")]
    [InlineData("IT_B6_056", "null")]
    [InlineData("IT_B6_056", "object")]
    [InlineData("IT_B6_056", "nobody")]
    public async Task IT_B6_056_Return_Without_Reason_Is_Rejected(string caseId, string kind)
    {
        var before = await Snap6();
        object? body = kind switch { "empty" => new { Reason = "" }, "null" => new { Reason = (string?)null }, "object" => "{}", _ => null };

        var r = await Call("manager", Post, $"{MRM}/{R.Mr1.MaterialReceiptId}/return", body);

        Graceful(r, kind == "nobody" ? new[] { 400, 415 } : new[] { 400 });
        (await MrAsync(R.Mr1.MaterialReceiptId))!.Status.Should().Be(ReceiptStatuses.Pending);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_057", 500)]
    [InlineData("IT_B6_057", 501)]
    public async Task IT_B6_057_Return_Reason_Length_Boundary(string caseId, int length)
    {
        var before = await Snap6();
        var reason = Vn(length);

        var r = await ReturnMr(R.Mr1.MaterialReceiptId, new { Reason = reason });

        if (length == 500)
        {
            r.Code.Should().Be(200, r.ToString());
            (await MrAsync(R.Mr1.MaterialReceiptId))!.ReturnReason.Should().Be(reason);
        }
        else
        {
            Graceful(r, 400);
            await Unchanged6(before);
        }
    }

    [Fact]
    public async Task IT_B6_058_Return_Confirmed_Receipt_Returns_409()
    {
        var before = await Snap6();

        var r = await ReturnMr(R.Mr3.MaterialReceiptId, new { Reason = "Muộn rồi" });

        Graceful(r, 409);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_060_Detail_Shows_Confirmer_Name_And_Material_Info_On_Both_Prefixes()
    {
        var id = R.Mr1.MaterialReceiptId;
        (await Confirm(id)).Code.Should().Be(200);
        var before = await Snap6();

        foreach (var url in new[] { $"{MRM}/{id}", $"{MRW}/{id}" })
        {
            var r = await Call(url.StartsWith(MRM) ? "manager" : "warehouse", Get, url);
            r.Code.Should().Be(200, r.ToString());
            B1Json.Str(r.Data, "confirmedByName").Should().Be(S0.Mgr.Employee.FullName);
            var line = B1Json.Prop(r.Data, "lines")!.Value.EnumerateArray().Single();
            B1Json.Str(line, "materialCode").Should().Be(B.Ma2.MaterialCode);
            B1Json.Str(line, "materialName").Should().Be(B.Ma2.MaterialName);
            B1Json.Str(line, "unit").Should().Be(B.Ma2.Unit);
            B1Json.Prop(line, "materialColor").Should().NotBeNull();
        }

        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_061_Detail_Of_Line_With_Null_MaterialId_Is_Not_500()
    {
        Db.MaterialReceiptLines.Add(new MaterialReceiptLine
        {
            MaterialReceiptId = R.Mr1.MaterialReceiptId, OrderMaterialId = R.Om2.OrderMaterialId, MaterialId = null, AcceptedQty = 5m,
        });
        await Db.SaveChangesAsync();
        var before = await Snap6();

        var r = await Call("manager", Get, $"{MRM}/{R.Mr1.MaterialReceiptId}");

        r.ShouldNotBeServerError();
        r.Code.Should().Be(200, r.ToString());
        await Unchanged6(before);
    }
}
