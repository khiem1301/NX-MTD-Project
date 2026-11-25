using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Batch4;
using Integration.Tests.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch5;

/// <summary>Nền chung IT_B5: S0 Batch 1 + B2 + B4 + S0 Batch 5 (<see cref="B5Context"/>, alias P). Mỗi test chạy trong ambient transaction và Rollback.</summary>
public abstract class B5TestBase : B4TestBase
{
    protected B5Context P { get; private set; } = null!;
    protected static readonly DateOnly T = FixedClock.Default.Today;
    protected static string PO => M + "purchase-orders";
    protected static string TRK => M + "purchase-order-tracking";
    protected static string PAY => AC + "purchase-payments";
    protected static string WKP => WK + "purchase-orders";
    protected static string SUP => A + "suppliers";

    protected B5TestBase(ITestOutputHelper output) : base(output) { }

    protected override async Task OnSeededAsync(TestDataSeeder seeder)
    {
        await base.OnSeededAsync(seeder);
        P = await seeder.SeedBatch5BaseAsync(B, D, S0);
    }

    // ------------------------------------------------------------------ snapshot / truy vấn DB

    protected async Task<Snap4> Snap5()
    {
        var s = await Snap();
        await using var db = NewDb();
        var pos = await db.PurchaseOrders.AsNoTracking().OrderBy(x => x.PurchaseOrderId).Select(x => new
        {
            x.PurchaseOrderId, x.PoCode, x.OrderId, x.SupplierId, x.OrderDate, x.ExpectedDeliveryCount, x.ExpectedDeliveryDate, x.ExpectedDeliveryDateTo,
            x.GoodsStatus, x.PaymentStatus, x.RejectReason, x.PaymentReturnReason, x.PaymentDate, x.PaidAmount, x.ActualAmount, x.PaymentMethod,
            x.PaymentNote, x.Note, x.RowVer,
        }).ToListAsync();
        var lines = await db.PurchaseOrderLines.AsNoTracking().OrderBy(x => x.PurchaseOrderLineId).Select(x => new
            { x.PurchaseOrderLineId, x.PurchaseOrderId, x.OrderMaterialId, x.MaterialId, x.OrderedQty, x.NotReplenishedQty, x.UnitPrice, x.Amount }).ToListAsync();
        var pays = await db.PurchasePayments.AsNoTracking().OrderBy(x => x.PurchasePaymentId).Select(x => new
            { x.PurchasePaymentId, x.PurchaseOrderId, x.PaymentDate, x.Amount, x.PaymentMethod, x.PaymentNote, x.CreatedAt, x.UpdatedAt, x.EditReason }).ToListAsync();
        var dsd = await db.DeliveryScheduleDates.AsNoTracking().Where(x => x.PurchaseOrderId != null).OrderBy(x => x.DeliveryScheduleDateId)
            .Select(x => new { x.DeliveryScheduleDateId, x.PurchaseOrderId, x.SeqNo, x.DeliveryDate }).ToListAsync();
        var sup = await db.Suppliers.AsNoTracking().OrderBy(x => x.SupplierId).Select(x => new
            { x.SupplierId, x.SupplierName, x.Phone, x.Email, x.Status, x.DiscontinueReason }).ToListAsync();
        return new Snap4(s.Counts, s.Detail + JsonSerializer.Serialize(new { pos, lines, pays, dsd, sup }));
    }

    protected async Task Unchanged(Snap4 before, string because = "")
    {
        await before.Counts.AssertUnchangedAsync(because);
        (await Snap5()).Detail.Should().Be(before.Detail, $"[DB] không được thay đổi. {because}");
    }

    protected async Task<PurchaseOrder> PoAsync(int id)
    {
        await using var db = NewDb();
        return await db.PurchaseOrders.AsNoTracking().SingleAsync(p => p.PurchaseOrderId == id);
    }

    protected async Task<PurchaseOrder?> PoByCodeAsync(string code)
    {
        await using var db = NewDb();
        return await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(p => p.PoCode == code);
    }

    protected async Task<byte[]> RvAsync(int poId) => (await PoAsync(poId)).RowVer;

    protected async Task<List<PurchaseOrderLine>> PolsAsync(int poId)
    {
        await using var db = NewDb();
        return await db.PurchaseOrderLines.AsNoTracking().Where(l => l.PurchaseOrderId == poId).OrderBy(l => l.PurchaseOrderLineId).ToListAsync();
    }

    protected async Task<PurchaseOrderLine> PolAsync(int lineId)
    {
        await using var db = NewDb();
        return await db.PurchaseOrderLines.AsNoTracking().SingleAsync(l => l.PurchaseOrderLineId == lineId);
    }

    protected async Task<List<PurchasePayment>> PaysAsync(int poId)
    {
        await using var db = NewDb();
        return await db.PurchasePayments.AsNoTracking().Where(p => p.PurchaseOrderId == poId).OrderBy(p => p.PurchasePaymentId).ToListAsync();
    }

    protected async Task<PurchasePayment> PayAsync(int id)
    {
        await using var db = NewDb();
        return await db.PurchasePayments.AsNoTracking().SingleAsync(p => p.PurchasePaymentId == id);
    }

    protected async Task<List<DeliveryScheduleDate>> PoDsdAsync(int poId)
    {
        await using var db = NewDb();
        return await db.DeliveryScheduleDates.AsNoTracking().Where(d => d.PurchaseOrderId == poId).OrderBy(d => d.SeqNo).ToListAsync();
    }

    protected async Task<Supplier> SupplierAsync(int id)
    {
        await using var db = NewDb();
        return await db.Suppliers.AsNoTracking().SingleAsync(s => s.SupplierId == id);
    }

    protected async Task<Supplier?> SupplierByNameAsync(string name)
    {
        await using var db = NewDb();
        return await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(s => s.SupplierName == name);
    }

    protected async Task<long> AuditMaxAsync()
    {
        await using var db = NewDb();
        return await db.AuditLogs.AsNoTracking().Select(a => (long?)a.AuditLogId).MaxAsync() ?? 0;
    }

    protected async Task<int> AuditCountAsync5()
    {
        await using var db = NewDb();
        return await db.AuditLogs.AsNoTracking().CountAsync();
    }

    protected async Task<Material?> MaterialByNameAsync(string name)
    {
        await using var db = NewDb();
        return await db.Materials.AsNoTracking().SingleOrDefaultAsync(m => m.MaterialName == name);
    }

    protected async Task<int> MissingSupplierIdAsync()
    {
        await using var db = NewDb();
        return (await db.Suppliers.AsNoTracking().MaxAsync(s => s.SupplierId)) + 1_000_000;
    }

    protected async Task<int> MissingPoIdAsync()
    {
        await using var db = NewDb();
        return (await db.PurchaseOrders.AsNoTracking().MaxAsync(s => s.PurchaseOrderId)) + 1_000_000;
    }

    protected async Task<int> MissingMaterialIdAsync()
    {
        await using var db = NewDb();
        return (await db.Materials.AsNoTracking().MaxAsync(s => s.MaterialId)) + 1_000_000;
    }

    protected async Task<int> MissingPayIdAsync()
    {
        await using var db = NewDb();
        return (await db.PurchasePayments.AsNoTracking().MaxAsync(s => (int?)s.PurchasePaymentId) ?? 0) + 1_000_000;
    }

    // ------------------------------------------------------------------ gọi API

    protected async Task Deny(string who, int[] codes, params (HttpMethod M, string Url)[] reqs)
    {
        var before = await Snap5();
        foreach (var (m, url) in reqs)
        {
            var r = await CallAs(who, m, url, m == Get ? null : new { });
            r.ShouldNotBeServerError();
            r.Code.Should().BeOneOf(codes, $"{who} {m} {url}: {r}");
        }

        await Unchanged(before);
    }

    protected static int PoIdOf(Api r) => B1Json.Int(r.Data, "purchaseOrderId") ?? -1;

    protected static string Rnd(int digits) => string.Concat(Enumerable.Range(0, digits).Select(_ => Random.Shared.Next(0, 10)));

    protected static string Fill(int len, string seed = "") => (seed + string.Concat(Enumerable.Repeat(Guid.NewGuid().ToString("N"), len / 32 + 1)))[..len];

    // ------------------------------------------------------------------ body mẫu

    protected static object PL(int materialId, decimal qty, decimal price) =>
        new { MaterialId = (int?)materialId, NewMaterialName = (string?)null, NewMaterialUnit = (string?)null, NewMaterialColor = (string?)null, OrderedQty = qty, UnitPrice = price };

    protected static object NewPL(string? name, string? unit, string? color, decimal qty, decimal price, int? materialId = null) =>
        new { MaterialId = materialId, NewMaterialName = name, NewMaterialUnit = unit, NewMaterialColor = color, OrderedQty = qty, UnitPrice = price };

    protected static DateOnly[] Dates(int n) => Enumerable.Range(1, n).Select(i => T.AddDays(7 * i)).ToArray();

    protected Dictionary<string, object?> Mk(object[]? lines = null, int? orderId = null, int? supplierId = null, int count = 1, DateOnly[]? dates = null,
        string? note = null) => new()
    {
        ["OrderId"] = orderId, ["SupplierId"] = supplierId ?? B.Su1.SupplierId, ["OrderDate"] = T, ["ExpectedDeliveryCount"] = count,
        ["Note"] = note, ["Lines"] = lines ?? [PL(B.Ma1.MaterialId, 10m, 10_000m)], ["ExpectedDeliveryDates"] = dates ?? Dates(count),
    };

    protected async Task<Api> Create(object body, string who = "manager") => await Call(who, Post, PO, body);

    protected static object PLine(int lineId, decimal qty, decimal price) => new { PurchaseOrderLineId = lineId, OrderedQty = qty, UnitPrice = price };

    protected Dictionary<string, object?> PlaceBody(byte[]? rowVer, object[] lines, int? supplierId = null, int count = 2, DateOnly[]? dates = null,
        string? note = "Chốt giá") => new()
    {
        ["SupplierId"] = supplierId ?? B.Su1.SupplierId, ["OrderDate"] = T, ["ExpectedDeliveryCount"] = count, ["Note"] = note, ["Lines"] = lines,
        ["RowVer"] = rowVer, ["ExpectedDeliveryDates"] = dates ?? Dates(count),
    };

    protected object[] Po1Lines(decimal q1 = 110m, decimal p1 = 49_500m) =>
        [PLine(P.Pol1.PurchaseOrderLineId, q1, p1), PLine(P.Pol2.PurchaseOrderLineId, 500m, 2_000m)];

    protected static Dictionary<string, object?> Rep(byte[]? rowVer, object[] lines, string? note = "NCC giảm giá") =>
        new() { ["Note"] = note, ["Lines"] = lines, ["RowVer"] = rowVer };

    protected static object RLine(int lineId, decimal price) => new { PurchaseOrderLineId = lineId, UnitPrice = price };

    protected static Dictionary<string, object?> Reason(byte[]? rowVer, string? reason) => new() { ["Reason"] = reason, ["RowVer"] = rowVer };

    protected static Dictionary<string, object?> Conf(byte[]? rowVer, decimal paid = 9_600_000m, string method = "BankTransfer", DateOnly? date = null,
        string? note = "UNC 123", decimal? actual = null) => new()
    {
        ["PaymentDate"] = date ?? T, ["PaidAmount"] = paid, ["PaymentMethod"] = method, ["PaymentNote"] = note, ["RowVer"] = rowVer, ["ActualAmount"] = actual,
    };

    protected static Dictionary<string, object?> Upd(byte[]? rowVer, decimal amount = 3_500_000m, string method = "Cash", DateOnly? date = null,
        string? note = "Sửa số", string? reason = "Nhập sai số tiền") => new()
    {
        ["PaymentDate"] = date ?? T.AddDays(-1), ["Amount"] = amount, ["PaymentMethod"] = method, ["PaymentNote"] = note, ["Reason"] = reason, ["RowVer"] = rowVer,
    };

    protected static string PayUrl(int poId, int payId) => $"{PAY}/{poId}/payments/{payId}";

    protected static List<JsonElement> Arr(JsonElement e, string name) =>
        B1Json.Prop(e, name) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray().ToList() : [];

    protected static HashSet<int> IdsOf(Api r, string name = "purchaseOrderId") =>
        r.Items().Select(i => B1Json.Int(i, name)).Where(v => v.HasValue).Select(v => v!.Value).ToHashSet();

    /// <summary>Đổi Note của PO trực tiếp bằng DB để RowVer client đang giữ trở nên cũ.</summary>
    protected async Task BumpAsync(int poId)
    {
        var p = await Db.PurchaseOrders.SingleAsync(x => x.PurchaseOrderId == poId);
        p.Note = "bump " + Guid.NewGuid().ToString("N")[..6];
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>Kế toán xác nhận thanh toán đủ PO2 qua API, trả về RowVer mới.</summary>
    protected async Task<byte[]> ConfirmPo2Async()
    {
        var r = await Call("accountant", Post, $"{PAY}/{P.Po2.PurchaseOrderId}/confirm", Conf(await RvAsync(P.Po2.PurchaseOrderId), actual: 9_600_000m));
        r.Code.Should().Be(200, r.ToString());
        return await RvAsync(P.Po2.PurchaseOrderId);
    }
}
