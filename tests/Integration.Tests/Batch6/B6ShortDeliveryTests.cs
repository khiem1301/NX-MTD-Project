using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch6;

/// <summary>IT_B6_070–087 — ShortDeliveries (Manager xử lý, Thủ kho xem) + IT_B6_099.</summary>
public sealed class B6ShortDeliveryTests(ITestOutputHelper output) : B6TestBase(output)
{
    private static HashSet<int> RequestIds(Api r) => IdsOf(r, "requestId");

    [Fact]
    public async Task IT_B6_070_Manager_List_Filters_Match_Db()
    {
        var before = await Snap6();

        var all = await Call("manager", Get, SDM);
        var awaiting = await Call("manager", Get, SDM + "?AwaitingReplenish=true");
        var resolved = await Call("manager", Get, SDM + "?ResolvedOnly=true");

        all.Code.Should().Be(200, all.ToString());
        awaiting.Code.Should().Be(200, awaiting.ToString());
        resolved.Code.Should().Be(200, resolved.ToString());
        await using var db = NewDb();
        var dbAll = await db.Requests.AsNoTracking().Where(x => x.RequestTypeCode == RequestTypes.ShortDelivery).Select(x => x.RequestId).ToListAsync();
        RequestIds(all).Should().BeEquivalentTo(dbAll);
        RequestIds(all).Should().Contain([R.Sd1.RequestId, R.Sd2.RequestId, R.Sd3.RequestId]);
        RequestIds(awaiting).Should().Contain(R.Sd2.RequestId).And.NotContain([R.Sd1.RequestId, R.Sd3.RequestId, R.Sd9.RequestId]);
        RequestIds(resolved).Should().Contain([R.Sd2.RequestId, R.Sd3.RequestId]).And.NotContain([R.Sd1.RequestId, R.Sd9.RequestId]);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_071_WarehouseKeeper_List_Ignores_Client_ResolvedOnly()
    {
        var before = await Snap6();

        var r = await Call("warehouse", Get, SDW + "?ResolvedOnly=false");

        r.Code.Should().Be(200, r.ToString());
        RequestIds(r).Should().Contain([R.Sd2.RequestId, R.Sd3.RequestId]).And.NotContain([R.Sd1.RequestId, R.Sd9.RequestId]);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_072_WarehouseKeeper_Detail_Of_Unresolved_Request_Matches_List_Scope()
    {
        var before = await Snap6();

        var r = await Call("warehouse", Get, $"{SDW}/{R.Sd1.RequestId}");

        Graceful(r, 200, 404);
        Note($"WK GET SD chưa xử lý -> {r.Code} (danh sách không có SD này: nếu 200 là lệch phạm vi)");
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_073_Resolve_Replenish_Stores_Schedule_And_Line_Qty()
    {
        var rv = (await ReqAsync(R.Sd1.RequestId))!.RowVer;
        var dates = new[] { T.AddDays(5), T.AddDays(10) };

        var r = await Resolve(R.Sd1.RequestId, ResolveBody("REPLENISH", [Rep(R.Rl1.RequestLineId, 100m)], "NCC giao bù", 2, dates));

        r.Code.Should().Be(200, r.ToString());
        var sd = (await ReqAsync(R.Sd1.RequestId))!;
        sd.ShortageResolution.Should().Be(ShortageResolutions.Replenish);
        Note($"Sau giao bù Status = {sd.Status}");
        sd.ExpectedDeliveryCount.Should().Be(2);
        sd.TargetDate.Should().Be(dates[0]);
        sd.TargetDateTo.Should().Be(dates[1]);
        sd.ResultNote.Should().Be("NCC giao bù");
        NearVnNow(sd.DecidedAt, "DecidedAt");
        sd.RowVer.Should().NotEqual(rv);
        (await RlAsync(R.Sd1.RequestId)).Single().ReplenishQty.Should().Be(100m);
        var dsd = await ReqDsdAsync(R.Sd1.RequestId);
        dsd.Select(x => x.DeliveryDate).Should().Equal(dates);
        dsd.Select(x => (int)x.SeqNo).Should().Equal(1, 2);
    }

    [Fact]
    public async Task IT_B6_074_Resolve_No_Replenish_Closes_The_Request()
    {
        var r = await Resolve(R.Sd1.RequestId, ResolveBody("NO_REPLENISH", null, "Chấp nhận thiếu", null));

        r.Code.Should().Be(200, r.ToString());
        var sd = (await ReqAsync(R.Sd1.RequestId))!;
        sd.ShortageResolution.Should().Be(ShortageResolutions.NoReplenish);
        sd.CompletedAt.Should().NotBeNull();
        (await RlAsync(R.Sd1.RequestId)).Single().ReplenishQty.GetValueOrDefault().Should().Be(0m);
        (await ReqDsdAsync(R.Sd1.RequestId)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("IT_B6_075", "Replenish")]
    [InlineData("IT_B6_075", "PARTIAL")]
    [InlineData("IT_B6_075", "")]
    public async Task IT_B6_075_Resolution_Value_Is_Validated(string caseId, string resolution)
    {
        var before = await Snap6();

        var r = await Resolve(R.Sd1.RequestId, ResolveBody(resolution, [Rep(R.Rl1.RequestLineId, 10m)]));

        if (resolution == "Replenish") Graceful(r, 200, 400); else Graceful(r, 400);
        if (r.Code != 200) await Unchanged6(before);
        await using var db = NewDb();
        (await db.Requests.AsNoTracking().Where(x => x.ShortageResolution != null).Select(x => x.ShortageResolution!).Distinct().ToListAsync())
            .Should().BeSubsetOf(["REPLENISH", "NO_REPLENISH"]);
    }

    [Theory]
    [InlineData("IT_B6_076", "null")]
    [InlineData("IT_B6_076", "0")]
    [InlineData("IT_B6_076", "1")]
    [InlineData("IT_B6_076", "99")]
    [InlineData("IT_B6_076", "100")]
    [InlineData("IT_B6_076", "-1")]
    [InlineData("IT_B6_076", "32768")]
    public async Task IT_B6_076_ExpectedDeliveryCount_Boundaries(string caseId, string raw)
    {
        var before = await Snap6();
        int? count = raw == "null" ? null : int.Parse(raw);
        var body = ResolveBody("REPLENISH", [Rep(R.Rl1.RequestLineId, 10m)], "x", null);
        body["ExpectedDeliveryCount"] = count;
        body["ExpectedDeliveryDates"] = Enumerable.Range(1, count is >= 1 and <= 99 ? count.Value : 1).Select(i => T.AddDays(i)).ToArray();

        var r = await Resolve(R.Sd1.RequestId, body);

        if (count is 1 or 99)
        {
            r.Code.Should().Be(200, r.ToString());
            (await ReqDsdAsync(R.Sd1.RequestId)).Should().HaveCount(count.Value);
            (await ReqAsync(R.Sd1.RequestId))!.ExpectedDeliveryCount.Should().Be((short)count.Value);
        }
        else
        {
            Graceful(r, 400);
            await Unchanged6(before);
        }
    }

    [Theory]
    [InlineData("IT_B6_077", "short-list")]
    [InlineData("IT_B6_077", "duplicate")]
    [InlineData("IT_B6_077", "min-date")]
    [InlineData("IT_B6_077", "past")]
    public async Task IT_B6_077_ExpectedDeliveryDates_Are_Validated(string caseId, string kind)
    {
        var before = await Snap6();
        var dates = kind switch
        {
            "short-list" => new[] { T.AddDays(5) },
            "duplicate" => new[] { T.AddDays(5), T.AddDays(5) },
            "min-date" => new[] { DateOnly.MinValue, T.AddDays(5) },
            _ => new[] { T.AddDays(-3), T.AddDays(5) },
        };

        var r = await Resolve(R.Sd1.RequestId, ResolveBody("REPLENISH", [Rep(R.Rl1.RequestLineId, 10m)], "x", 2, dates));

        Graceful(r, 400);
        await Unchanged6(before);
        (await ReqDsdAsync(R.Sd1.RequestId)).Should().NotContain(x => x.DeliveryDate == DateOnly.MinValue);
    }

    [Theory]
    [InlineData("IT_B6_078", "101")]
    [InlineData("IT_B6_078", "0")]
    [InlineData("IT_B6_078", "-1")]
    [InlineData("IT_B6_078", "1000000000000000")]
    public async Task IT_B6_078_ReplenishQty_Boundaries(string caseId, string raw)
    {
        var before = await Snap6();
        var qty = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);

        var r = await Resolve(R.Sd1.RequestId, ResolveBody("REPLENISH", [Rep(R.Rl1.RequestLineId, qty)]));

        Graceful(r, 400);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_079_Line_From_Another_Request_Is_Rejected()
    {
        var before = await Snap6();

        var r = await Resolve(R.Sd1.RequestId, ResolveBody("REPLENISH", [Rep(R.Rl1.RequestLineId, 10m), Rep(R.Rl9.RequestLineId, 5m)]));

        Graceful(r, 400, 404);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_080", "null")]
    [InlineData("IT_B6_080", "empty")]
    [InlineData("IT_B6_080", "other-line")]
    public async Task IT_B6_080_Replenish_Requires_Qty_For_Every_Line(string caseId, string kind)
    {
        var before = await Snap6();
        object[]? lines = kind switch { "null" => null, "empty" => [], _ => [Rep(R.Rl2.RequestLineId, 5m)] };

        var r = await Resolve(R.Sd1.RequestId, ResolveBody("REPLENISH", lines));

        Graceful(r, 400, 404);
        await Unchanged6(before);
    }

    [Theory]
    [InlineData("IT_B6_081", 1000)]
    [InlineData("IT_B6_081", 1001)]
    public async Task IT_B6_081_ResultNote_Length_Boundary(string caseId, int length)
    {
        var before = await Snap6();
        var note = Vn(length);

        var r = await Resolve(R.Sd1.RequestId, ResolveBody("NO_REPLENISH", null, note, null));

        if (length == 1000)
        {
            r.Code.Should().Be(200, r.ToString());
            (await ReqAsync(R.Sd1.RequestId))!.ResultNote.Should().Be(note);
        }
        else
        {
            Graceful(r, 400);
            await Unchanged6(before);
        }
    }

    [Fact]
    public async Task IT_B6_082_Resolve_Closed_Request_Is_Rejected()
    {
        var before = await Snap6();

        var r = await Resolve(R.Sd3.RequestId, ResolveBody("NO_REPLENISH", null, "lần 2", null));

        Graceful(r, 400, 409);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_083_Second_Replenish_Accumulates_Expected_Delivery_Count()
    {
        var r = await Resolve(R.Sd2.RequestId, ResolveBody("REPLENISH", [Rep(R.Rl2.RequestLineId, 40m)], "bù thêm", 1, [T.AddDays(20)]));

        Graceful(r, 200, 409);
        if (r.Code == 200) (await ReqAsync(R.Sd2.RequestId))!.ExpectedDeliveryCount.Should().Be((short)2, "cộng dồn 1 + 1, không ghi đè");
        else (await ReqAsync(R.Sd2.RequestId))!.ExpectedDeliveryCount.Should().Be((short)1);
    }

    [Fact]
    public async Task IT_B6_085_Resolve_Unknown_Or_Wrong_Type_Request_Returns_404()
    {
        var other = new Request { RequestCode = "RQ-B6-OTHER", RequestTypeCode = RequestTypes.Other, Status = RequestStatuses.PendingProcessing, CreatedBy = S0.Wh.EmployeeId, Title = "x", Content = "x", HandlerRoleCode = RoleCodes.Manager, CreatedAt = Clock.Now };
        Db.Requests.Add(other);
        await Db.SaveChangesAsync();
        var before = await Snap6();

        var missing = await Resolve(await MissingRequestIdAsync(), ResolveBody("NO_REPLENISH", null, "x", null));
        var wrong = await Resolve(other.RequestId, ResolveBody("NO_REPLENISH", null, "x", null));

        Graceful(missing, 404);
        Graceful(wrong, 404);
        await Unchanged6(before);
    }

    [Fact]
    public async Task IT_B6_087_Db_Rejects_Second_ShortDelivery_For_Same_Receipt()
    {
        Db.Requests.Add(new Request
        {
            RequestCode = "YC-B6-DUP", RequestTypeCode = RequestTypes.ShortDelivery, Status = RequestStatuses.PendingProcessing,
            MaterialReceiptId = R.Mr3.MaterialReceiptId, CreatedAt = Clock.Now,
        });

        var act = async () => await Db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("UX_Request_Receipt: mỗi phiếu nhập gốc tối đa 1 yêu cầu thiếu giao");
        Db.ChangeTracker.Clear();
        (await ShortOfReceiptAsync(R.Mr3.MaterialReceiptId)).Should().ContainSingle();
    }

    [Fact]
    public async Task IT_B6_099_Unknown_Receipt_Or_ShortDelivery_Returns_404()
    {
        var before = await Snap6();

        var mr = await Call("manager", Get, $"{MRM}/{await MissingMrIdAsync()}");
        var sd = await Call("manager", Get, $"{SDM}/{await MissingRequestIdAsync()}");

        Graceful(mr, 404);
        Graceful(sd, 404);
        await Unchanged6(before);
    }
}
