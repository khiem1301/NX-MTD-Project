using System.Reflection;
using FluentAssertions;
using Integration.Tests.Batch1;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

/// <summary>IT_B5_116–120, 122 — kiểm tra chéo trạng thái, số liệu suy ra, nhật ký, upload.</summary>
public sealed class B5CrossCheckTests(ITestOutputHelper output) : B5TestBase(output)
{
    [Fact]
    public async Task IT_B5_116_Status_Columns_Are_Upper_Snake()
    {
        var create = await Create(Mk());
        Created(create);
        (await Call("manager", Post, $"{TRK}/{P.Po1.PurchaseOrderId}/place", PlaceBody(await RvAsync(P.Po1.PurchaseOrderId), Po1Lines()))).Code.Should().Be(200);
        await ConfirmPo2Async();

        await using var db = NewDb();
        var goods = await db.PurchaseOrders.AsNoTracking().Select(p => p.GoodsStatus).Distinct().ToListAsync();
        var pay = await db.PurchaseOrders.AsNoTracking().Select(p => p.PaymentStatus).Distinct().ToListAsync();
        var sup = await db.Suppliers.AsNoTracking().Select(s => s.Status).Distinct().ToListAsync();
        foreach (var v in goods.Concat(pay).Concat(sup)) v.Should().MatchRegex("^[A-Z_]+$", "trạng thái lưu UPPER_SNAKE");
        goods.Should().Contain(["ORDERED", "PENDING_FINALISATION", "PARTIALLY_RECEIVED"]);
        pay.Should().Contain(["UNPAID", "PAID"]);
        sup.Should().Contain("ACTIVE");
    }

    [Fact]
    public async Task IT_B5_117_Flat_Payment_Columns_Match_Payment_Table()
    {
        await ConfirmPo2Async();
        (await Call("accountant", Put, PayUrl(P.Po3.PurchaseOrderId, P.Pp2.PurchasePaymentId), Upd(await RvAsync(P.Po3.PurchaseOrderId)))).Code.Should().Be(200);

        await using var db = NewDb();
        var pos = await db.PurchaseOrders.AsNoTracking().Where(p => db.PurchasePayments.Any(x => x.PurchaseOrderId == p.PurchaseOrderId)).ToListAsync();
        pos.Should().NotBeEmpty();
        foreach (var po in pos)
        {
            var pays = await PaysAsync(po.PurchaseOrderId);
            po.PaidAmount.Should().Be(pays.Sum(p => p.Amount), po.PoCode);
            po.PaymentMethod.Should().Be(pays.OrderByDescending(p => p.PurchasePaymentId).First().PaymentMethod, po.PoCode);
        }

        var e12 = await Call("accountant", Get, $"{PAY}/{P.Po3.PurchaseOrderId}");
        B1Json.Dec(e12.Data, "paidAmount").Should().Be((await PaysAsync(P.Po3.PurchaseOrderId)).Sum(p => p.Amount));
        Arr(e12.Data, "payments").Sum(p => B1Json.Dec(p, "amount") ?? 0m).Should().Be(B1Json.Dec(e12.Data, "paidAmount"));
    }

    [Fact]
    public async Task IT_B5_118_Totals_And_Amounts_Match_Lines()
    {
        var trk = await Call("manager", Get, $"{TRK}?pageSize=100");
        var pay = await Call("accountant", Get, $"{PAY}?pageSize=100");

        trk.Code.Should().Be(200, trk.ToString());
        pay.Code.Should().Be(200, pay.ToString());
        await using var db = NewDb();
        foreach (var item in trk.Items().Concat(pay.Items()))
        {
            var id = B1Json.Int(item, "purchaseOrderId")!.Value;
            var lines = await PolsAsync(id);
            B1Json.Dec(item, "totalAmount").Should().Be(lines.Sum(l => l.Amount), $"PO {id}");
        }

        var all = await db.PurchaseOrderLines.AsNoTracking().ToListAsync();
        all.Should().OnlyContain(l => l.Amount == decimal.Round(l.OrderedQty * l.UnitPrice, 2));
    }

    [Fact]
    public async Task IT_B5_119_Failed_Operations_Leave_No_Audit_Or_Partial_Data()
    {
        var audit = await AuditMaxAsync();
        var before = await Snap5();
        var stale = await RvAsync(P.Po1.PurchaseOrderId);

        var r025 = await Create(Mk([PL(B.Ma1.MaterialId, 5m, 1_000m), PL(B.Ma1.MaterialId, 6m, 1_000m)]));
        var r031 = await Create(Mk([NewPL("Ren hỏng " + Rnd(5), "m", null, 5m, 1_000m), PL(B.Ma1.MaterialId, -1m, 1_000m)]));
        var r091 = await Call("accountant", Post, $"{PAY}/{P.Po2.PurchaseOrderId}/confirm", Conf(await RvAsync(P.Po2.PurchaseOrderId), 0m));
        var r102 = await Call("accountant", Put, PayUrl(P.Po3.PurchaseOrderId, P.Pp1.PurchasePaymentId), Upd(await RvAsync(P.Po3.PurchaseOrderId)));
        var r104 = await Call("accountant", Put, PayUrl(P.Po3.PurchaseOrderId, P.Pp9.PurchasePaymentId), Upd(await RvAsync(P.Po3.PurchaseOrderId)));
        await BumpAsync(P.Po1.PurchaseOrderId);
        var afterBump = await Snap5();
        var r061 = await Call("manager", Post, $"{TRK}/{P.Po1.PurchaseOrderId}/place", PlaceBody(stale, Po1Lines()));

        foreach (var r in new[] { r025, r031, r091, r102, r104, r061 }) { r.ShouldNotBeServerError(); r.Code.Should().BeInRange(400, 499, r.ToString()); }
        (await AuditMaxAsync()).Should().Be(audit);
        await Unchanged(afterBump);
        before.Counts.Values["Materials"].Should().Be((await Snap5()).Counts.Values["Materials"]);
    }

    [Fact]
    public async Task IT_B5_120_Full_Lifecycle_Chain()
    {
        var audit = await AuditMaxAsync();

        var create = await Create(Mk([PL(B.Ma1.MaterialId, 10m, 10_000m)], count: 1));
        Created(create);
        var id = PoIdOf(create);
        var rv0 = await RvAsync(id);
        var lineId = (await PolsAsync(id))[0].PurchaseOrderLineId;

        var place = await Call("manager", Post, $"{TRK}/{id}/place", PlaceBody(rv0, [PLine(lineId, 10m, 10_000m)], count: 1));
        var rv1 = await RvAsync(id);
        var reprice = await Call("manager", Post, $"{TRK}/{id}/reprice", Rep(rv1, [RLine(lineId, 9_000m)]));
        var rv2 = await RvAsync(id);
        var confirm = await Call("accountant", Post, $"{PAY}/{id}/confirm", Conf(rv2, 90_000m, actual: 90_000m));
        var rv3 = await RvAsync(id);
        var payId = (await PaysAsync(id)).Single().PurchasePaymentId;
        var edit = await Call("accountant", Put, PayUrl(id, payId), Upd(rv3, 90_000m, date: T));
        var rv4 = await RvAsync(id);
        var ret = await Call("accountant", Post, $"{PAY}/{id}/return", Reason(rv4, "Sai tài khoản"));

        place.Code.Should().Be(200, place.ToString());
        reprice.Code.Should().Be(200, reprice.ToString());
        confirm.Code.Should().Be(200, confirm.ToString());
        edit.Code.Should().Be(200, edit.ToString());
        ret.Code.Should().Be(200, ret.ToString());
        new[] { rv0, rv1, rv2, rv3, rv4 }.Select(Convert.ToHexString).Should().OnlyHaveUniqueItems("RowVer đổi sau mỗi bước ghi");
        (await AuditMaxAsync()).Should().BeGreaterThan(audit);
        var po = await PoAsync(id);
        po.GoodsStatus.Should().Be("ORDERED");
        po.PaymentStatus.Should().Be("RETURNED");
    }

    [Fact]
    public async Task IT_B5_122_No_Multipart_Endpoints_In_Batch()
    {
        var before = await Snap5();
        var names = new[] { "PurchaseOrdersController", "PurchaseOrderTrackingController", "PurchasePaymentsController", "SuppliersController" };
        var controllers = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a =>
        {
            try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t is not null)!; }
        }).Where(t => t.Namespace?.StartsWith("NX_MTD.API.Controllers") == true && names.Contains(t.Name)).ToList();
        controllers.Should().NotBeEmpty();
        controllers.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetParameters()).Should().NotContain(p => p.ParameterType.Name == "IFormFile");

        var r = await UploadAsync($"{PAY}/{P.Po2.PurchaseOrderId}/confirm", await TkAcc(), [1, 2, 3], "chung-tu.jpg", "image/jpeg");

        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(400, 415);
        await Unchanged(before);
    }
}
