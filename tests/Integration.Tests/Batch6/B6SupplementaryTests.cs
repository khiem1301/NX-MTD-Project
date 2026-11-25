using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>IT_B6_062–068 — POST WK/MaterialReceipts/supplementary.</summary>
public sealed class B6SupplementaryTests(ITestOutputHelper output) : B6TestBase(output)
{
    private Task<Api> Supp(object body, string who = "warehouse") => Call(who, Post, MRW + "/supplementary", body);

    [Fact]
    public async Task IT_B6_062_Create_Supplementary_Receipt_For_Replenish_Request()
    {
        var maxId = await MaxMrIdAsync();
        var stMax = await MaxStAsync();
        var onHand = await OnHandAsync(B.Ma1.MaterialId);

        var r = await Supp(SuppBody(note: "Bù đợt 1"));

        r.Code.Should().Be(200, r.ToString());
        B1Json.Str(r.Data, "receiptType")!.Should().BeOneOf("SUPPLEMENTARY", "Supplementary", "Bổ sung");
        B1Json.Int(r.Data, "shortageRequestId").Should().Be(R.Sd2.RequestId);
        var mr = await NewestMrAsync(maxId);
        mr.ShortageRequestId.Should().Be(R.Sd2.RequestId);
        mr.Status.Should().Be(ReceiptStatuses.Pending);
        var line = (await MrlAsync(mr.MaterialReceiptId)).Should().ContainSingle().Subject;
        line.AcceptedQty.Should().Be(40m);
        Note($"Source={mr.Source}, PO={mr.PurchaseOrderId}, OriginalReceiptLineId={line.OriginalReceiptLineId}, OriginalReceiptId={B1Json.Int(r.Data, "originalReceiptId")}");
        (await OnHandAsync(B.Ma1.MaterialId)).Should().Be(onHand);
        (await MaxStAsync()).Should().Be(stMax);
    }

    [Fact]
    public async Task IT_B6_063_Confirm_Supplementary_Receipt_Adds_Stock_And_Replenished_Qty()
    {
        var created = await Supp(SuppBody());
        created.Code.Should().Be(200, created.ToString());
        var id = ReceiptIdOf(created);
        await SeedUploadAsync(AttachmentOwnerTypes.MaterialReceipt, id, "warehouse", "DOCUMENT_PHOTO");
        var onHand = await OnHandAsync(B.Ma1.MaterialId);
        await using var dbCount = NewDb();
        var shortBefore = await dbCount.Requests.CountAsync(x => x.RequestTypeCode == RequestTypes.ShortDelivery);

        var r = await Confirm(id);

        r.Code.Should().Be(200, r.ToString());
        var detail = await Call("manager", Get, $"{SDM}/{R.Sd2.RequestId}");
        detail.Code.Should().Be(200, detail.ToString());
        var rl2 = B1Json.Prop(detail.Data, "lines")!.Value.EnumerateArray().Single(l => B1Json.Int(l, "requestLineId") == R.Rl2.RequestLineId);
        B1Json.Dec(rl2, "replenishedQty").Should().Be(40m);
        (await OnHandAsync(B.Ma1.MaterialId)).Should().Be(onHand + 40m);
        (await StAsync(id)).Should().ContainSingle().Which.QtyChange.Should().Be(40m);
        var sd2 = (await ReqAsync(R.Sd2.RequestId))!;
        Note($"Sd2 sau bù đủ: Status={sd2.Status}, CompletedAt={sd2.CompletedAt:O}");
        sd2.CompletedAt.Should().NotBeNull("đã bù đủ thì yêu cầu hoàn tất");
        await using var db2 = NewDb();
        (await db2.Requests.CountAsync(x => x.RequestTypeCode == RequestTypes.ShortDelivery)).Should().Be(shortBefore, "không sinh thêm SHORT_DELIVERY");
    }

    [Theory]
    [InlineData("IT_B6_064", "unresolved")]
    [InlineData("IT_B6_064", "no-replenish")]
    public async Task IT_B6_064_Only_Replenish_Requests_Accept_Supplementary(string caseId, string kind)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var body = SuppBody(kind == "unresolved" ? R.Sd1.RequestId : R.Sd3.RequestId, [SuppLine(kind == "unresolved" ? R.Rl1.RequestLineId : R.Rl3.RequestLineId, 5m)]);

        var r = await Supp(body);

        Graceful(r, 400, 409);
        (await MaxMrIdAsync()).Should().Be(maxId);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_065_Line_From_Another_Request_Is_Rejected()
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();

        var r = await Supp(SuppBody(lines: [SuppLine(R.Rl2.RequestLineId, 10m), SuppLine(R.Rl9.RequestLineId, 5m)]));

        Graceful(r, 400, 404);
        (await MaxMrIdAsync()).Should().Be(maxId);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_066", "41")]
    [InlineData("IT_B6_066", "0")]
    [InlineData("IT_B6_066", "-1")]
    public async Task IT_B6_066_Supplementary_Qty_Boundaries(string caseId, string raw)
    {
        var qty = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();

        var r = await Supp(SuppBody(lines: [SuppLine(R.Rl2.RequestLineId, qty)]));

        if (raw == "41") Graceful(r, 200, 400); else Graceful(r, 400);
        if (r.Code != 200) { (await MaxMrIdAsync()).Should().Be(maxId); await Unchanged6(before); }
        else (await MrlAsync((await NewestMrAsync(maxId)).MaterialReceiptId)).Should().OnlyContain(l => l.AcceptedQty > 0m);
    }

    [Theory]
    [InlineData("IT_B6_067", "duplicate")]
    [InlineData("IT_B6_067", "empty")]
    [InlineData("IT_B6_067", "unknown-request")]
    public async Task IT_B6_067_Invalid_Supplementary_Shapes(string caseId, string kind)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var body = kind switch
        {
            "duplicate" => SuppBody(lines: [SuppLine(R.Rl2.RequestLineId, 10m), SuppLine(R.Rl2.RequestLineId, 10m)]),
            "empty" => SuppBody(lines: []),
            _ => SuppBody(await MissingRequestIdAsync()),
        };

        var r = await Supp(body);

        if (kind == "duplicate") Graceful(r, 400, 409); else if (kind == "empty") Graceful(r, 400); else Graceful(r, 404);
        (await MaxMrIdAsync()).Should().Be(maxId);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_068", "date")]
    [InlineData("IT_B6_068", "note")]
    public async Task IT_B6_068_Supplementary_Date_And_Note_Boundaries(string caseId, string kind)
    {
        var maxId = await MaxMrIdAsync();
        var before = await Snap6();
        var body = kind == "date" ? SuppBody(date: DateOnly.MinValue) : SuppBody(note: Vn(1001));

        var r = await Supp(body);

        Graceful(r, 400);
        (await MaxMrIdAsync()).Should().Be(maxId);
        await Unchanged6(before);
    }
}
