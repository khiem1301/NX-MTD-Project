using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Batch4;
using Integration.Tests.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch7;

public sealed record Snap7(Snap4 Base, string Detail);

/// <summary>Nền chung IT_B7: S0 Batch 1 + B2 + B4 + S0 Batch 7 (<see cref="B7Context"/>, alias K). Mỗi test chạy trong ambient transaction và Rollback.</summary>
public abstract class B7TestBase : B4TestBase
{
    protected const string TLP = "/api/v1/team-leader/";
    protected static readonly DateOnly T = FixedClock.Default.Today;
    protected static string WOM => M + "work-orders";
    protected static string WOT => TLP + "work-orders";
    protected static string WOW => WK + "work-orders";
    protected static string CLT => TLP + "cutting-lays";
    protected static string CHT => TLP + "cut-handovers";
    protected static string CHW => WK + "cut-handovers";

    protected B7Context K { get; private set; } = null!;

    protected B7TestBase(ITestOutputHelper output) : base(output) { }

    protected override async Task OnSeededAsync(TestDataSeeder seeder)
    {
        await base.OnSeededAsync(seeder);
        K = await seeder.SeedBatch7BaseAsync(B, D, S0);
    }

    // ------------------------------------------------------------------ HTTP theo vai trò (TK_*)

    /// <summary>who: anonymous, admin, manager, accountant, warehouse, worker, qaqc, tl1 (TL tổ T_CUT01), td1, tl2 (T_CUT02), tlsew (tổ SEWING), tlnone, exp, forged.</summary>
    protected async Task<Api> Req(string who, HttpMethod m, string url, object? body = null) => who switch
    {
        "tl1" => await Call("tl02", m, url, body),
        "tlsew" => await Call("tl01", m, url, body),
        "td1" => await SendAsync(m, url, await TokenAsync(K.Td1), body),
        "tl2" => await SendAsync(m, url, await TokenAsync(K.Tl2), body),
        "tlnone" => await SendAsync(m, url, await TokenAsync(K.TlNone), body),
        "exp" => await SendAsync(m, url, await TkExpired(), body),
        "forged" => await SendAsync(m, url, await TkForged(), body),
        _ => await CallAs(who, m, url, body),
    };

    protected Task<Api> Do(string who, HttpMethod m, string url, object? body = null) => Req(who, m, url, body);

    // ------------------------------------------------------------------ snapshot DB

    protected async Task<Snap7> S7()
    {
        var s = await Snap();
        await using var db = NewDb();
        var wo = await db.WorkOrders.AsNoTracking().OrderBy(x => x.WorkOrderId).Select(x => new
            { x.WorkOrderId, x.WorkOrderCode, x.OrderId, x.ProductId, x.CuttingTeamId, x.SourceRequestId, x.DueDate, x.Status, x.Note, x.FabricTolerancePercent, x.CompletedAt }).ToListAsync();
        var wol = await db.WorkOrderLines.AsNoTracking().OrderBy(x => x.WorkOrderLineId).Select(x => new
            { x.WorkOrderLineId, x.WorkOrderId, x.OrderLineId, x.ProductComponentMaterialId, x.PlannedQty }).ToListAsync();
        var cl = await db.CuttingLays.AsNoTracking().OrderBy(x => x.CuttingLayId).Select(x => new
            { x.CuttingLayId, x.WorkOrderId, x.CuttingTeamId, x.WorkDate, x.ShiftCode, x.LaySequence, x.Note, x.Status }).ToListAsync();
        var cll = await db.CuttingLayLines.AsNoTracking().OrderBy(x => x.CuttingLayLineId).Select(x => new
            { x.CuttingLayLineId, x.CuttingLayId, x.OrderLineId, x.ProductComponentMaterialId, x.ActualQty }).ToListAsync();
        var clf = await db.CuttingLayFabrics.AsNoTracking().OrderBy(x => x.CuttingLayFabricId).Select(x => new
            { x.CuttingLayFabricId, x.CuttingLayId, x.MaterialId, x.UsedM }).ToListAsync();
        var ch = await db.CutHandovers.AsNoTracking().OrderBy(x => x.CutHandoverId).Select(x => new
            { x.CutHandoverId, x.HandoverCode, x.WorkOrderId, x.Status, x.ConfirmedBy, x.ConfirmedAt, x.RejectReason }).ToListAsync();
        var chl = await db.CutHandoverLines.AsNoTracking().OrderBy(x => x.CutHandoverLineId).Select(x => new
            { x.CutHandoverLineId, x.CutHandoverId, x.OrderLineId, x.ProductComponentMaterialId, x.HandedQty, x.ConfirmedQty, x.DiscrepancyReason }).ToListAsync();
        var sb = await db.StockBalances.AsNoTracking().OrderBy(x => x.StockBalanceId).Select(x => new
            { x.StockBalanceId, x.WarehouseType, x.MaterialId, x.OrderLineId, x.ProductComponentMaterialId, x.OnHandQty }).ToListAsync();
        var st = await db.StockTransactions.AsNoTracking().OrderBy(x => x.StockTransactionId).Select(x => new { x.StockTransactionId, x.QtyChange }).ToListAsync();
        var rq = await db.Requests.AsNoTracking().OrderBy(x => x.RequestId).Select(x => new { x.RequestId, x.Status, x.WorkOrderId, x.RowVer }).ToListAsync();
        return new Snap7(s, JsonSerializer.Serialize(new { wo, wol, cl, cll, clf, ch, chl, sb, st, rq }));
    }

    protected async Task Same7(Snap7 before, string because = "")
    {
        await before.Base.Counts.AssertUnchangedAsync(because);
        var after = await S7();
        after.Base.Detail.Should().Be(before.Base.Detail, $"[DB] không được thay đổi. {because}");
        after.Detail.Should().Be(before.Detail, $"[DB] 7 bảng batch + kho + Request không được thay đổi. {because}");
    }

    /// <summary>Lần lượt gửi các request; mỗi request phải trả một trong <paramref name="codes"/> (không 5xx, không lộ constraint) và DB không đổi.</summary>
    protected async Task Deny7(string who, int[] codes, params (HttpMethod M, string Url, object? Body)[] reqs)
    {
        var before = await S7();
        foreach (var (m, url, body) in reqs)
        {
            var r = await Req(who, m, url, body);
            Rejected(r, codes);
        }

        await Same7(before);
    }

    // ------------------------------------------------------------------ truy vấn DB

    protected async Task<WorkOrder> WoAsync(int id)
    {
        await using var db = NewDb();
        return await db.WorkOrders.AsNoTracking().SingleAsync(x => x.WorkOrderId == id);
    }

    protected async Task<List<WorkOrder>> WoAllAsync()
    {
        await using var db = NewDb();
        return await db.WorkOrders.AsNoTracking().OrderBy(x => x.WorkOrderId).ToListAsync();
    }

    protected async Task<int> MaxWoIdAsync()
    {
        await using var db = NewDb();
        return await db.WorkOrders.AsNoTracking().MaxAsync(x => x.WorkOrderId);
    }

    protected async Task<List<WorkOrder>> WoAfterAsync(int maxBefore)
    {
        await using var db = NewDb();
        return await db.WorkOrders.AsNoTracking().Where(x => x.WorkOrderId > maxBefore).OrderBy(x => x.WorkOrderId).ToListAsync();
    }

    protected async Task<List<WorkOrderLine>> WolsAsync(int woId)
    {
        await using var db = NewDb();
        return await db.WorkOrderLines.AsNoTracking().Where(x => x.WorkOrderId == woId).OrderBy(x => x.WorkOrderLineId).ToListAsync();
    }

    protected async Task<WorkOrderLine> WolAsync(int id)
    {
        await using var db = NewDb();
        return await db.WorkOrderLines.AsNoTracking().SingleOrDefaultAsync(x => x.WorkOrderLineId == id) ?? new WorkOrderLine { WorkOrderLineId = -1 };
    }

    /// <summary>Tổng kế hoạch cả bộ (PCM NULL) của một dòng đơn trên các lệnh chưa hủy.</summary>
    protected async Task<int> PlanSumAsync(int orderLineId)
    {
        await using var db = NewDb();
        return await (from l in db.WorkOrderLines.AsNoTracking()
                      join w in db.WorkOrders.AsNoTracking() on l.WorkOrderId equals w.WorkOrderId
                      where l.OrderLineId == orderLineId && l.ProductComponentMaterialId == null && w.Status != WorkOrderStatuses.Cancelled
                      select l.PlannedQty).SumAsync();
    }

    protected async Task<CuttingLay> LayAsync(int id)
    {
        await using var db = NewDb();
        return await db.CuttingLays.AsNoTracking().SingleAsync(x => x.CuttingLayId == id);
    }

    protected async Task<List<CuttingLay>> LaysAsync(int woId)
    {
        await using var db = NewDb();
        return await db.CuttingLays.AsNoTracking().Where(x => x.WorkOrderId == woId).OrderBy(x => x.CuttingLayId).ToListAsync();
    }

    protected async Task<int> MaxLayIdAsync()
    {
        await using var db = NewDb();
        return await db.CuttingLays.AsNoTracking().MaxAsync(x => x.CuttingLayId);
    }

    protected async Task<List<CuttingLay>> LaysAfterAsync(int maxBefore)
    {
        await using var db = NewDb();
        return await db.CuttingLays.AsNoTracking().Where(x => x.CuttingLayId > maxBefore).OrderBy(x => x.CuttingLayId).ToListAsync();
    }

    protected async Task<List<CuttingLayLine>> LayLinesAsync(int layId)
    {
        await using var db = NewDb();
        return await db.CuttingLayLines.AsNoTracking().Where(x => x.CuttingLayId == layId).OrderBy(x => x.CuttingLayLineId).ToListAsync();
    }

    protected async Task<List<CuttingLayFabric>> LayFabricsAsync(int layId)
    {
        await using var db = NewDb();
        return await db.CuttingLayFabrics.AsNoTracking().Where(x => x.CuttingLayId == layId).OrderBy(x => x.CuttingLayFabricId).ToListAsync();
    }

    protected async Task<int> CutSumAsync(int woId, int orderLineId)
    {
        await using var db = NewDb();
        return await (from l in db.CuttingLayLines.AsNoTracking()
                      join c in db.CuttingLays.AsNoTracking() on l.CuttingLayId equals c.CuttingLayId
                      where c.WorkOrderId == woId && l.OrderLineId == orderLineId
                      select l.ActualQty).SumAsync();
    }

    protected async Task<CutHandover> ChAsync(int id)
    {
        await using var db = NewDb();
        return await db.CutHandovers.AsNoTracking().SingleAsync(x => x.CutHandoverId == id);
    }

    protected async Task<int> MaxChIdAsync()
    {
        await using var db = NewDb();
        return await db.CutHandovers.AsNoTracking().MaxAsync(x => x.CutHandoverId);
    }

    protected async Task<List<CutHandover>> ChAfterAsync(int maxBefore)
    {
        await using var db = NewDb();
        return await db.CutHandovers.AsNoTracking().Where(x => x.CutHandoverId > maxBefore).OrderBy(x => x.CutHandoverId).ToListAsync();
    }

    protected async Task<List<CutHandoverLine>> ChlsAsync(int handoverId)
    {
        await using var db = NewDb();
        return await db.CutHandoverLines.AsNoTracking().Where(x => x.CutHandoverId == handoverId).OrderBy(x => x.CutHandoverLineId).ToListAsync();
    }

    /// <summary>Tổng số đã giao (phiếu chờ nhận + đã nhận) của một dòng đơn trong lệnh.</summary>
    protected async Task<int> HandedSumAsync(int woId, int orderLineId)
    {
        await using var db = NewDb();
        return await (from l in db.CutHandoverLines.AsNoTracking()
                      join h in db.CutHandovers.AsNoTracking() on l.CutHandoverId equals h.CutHandoverId
                      where h.WorkOrderId == woId && l.OrderLineId == orderLineId && (h.Status == HandoverStatuses.Pending || h.Status == HandoverStatuses.Confirmed)
                      select l.HandedQty).SumAsync();
    }

    protected async Task<decimal> BtpOnHandAsync(int orderLineId, int? pcmId)
    {
        await using var db = NewDb();
        return await db.StockBalances.AsNoTracking()
            .Where(x => x.WarehouseType == WarehouseTypes.SemiFinished && x.OrderLineId == orderLineId && x.ProductComponentMaterialId == pcmId)
            .Select(x => (decimal?)x.OnHandQty).SingleOrDefaultAsync() ?? 0m;
    }

    /// <summary>Tổng tồn BTP của một dòng đơn trên mọi định mức.</summary>
    protected async Task<decimal> BtpTotalAsync(int orderLineId)
    {
        await using var db = NewDb();
        return await db.StockBalances.AsNoTracking()
            .Where(x => x.WarehouseType == WarehouseTypes.SemiFinished && x.OrderLineId == orderLineId)
            .Select(x => (decimal?)x.OnHandQty).SumAsync() ?? 0m;
    }

    /// <summary>Thêm bàn cắt (RECORDED, T_CUT01 mặc định) trực tiếp vào DB kèm dòng cắt và vải Ma1.</summary>
    protected async Task<CuttingLay> AddLayAsync(int woId, string shift, short seq, params (int OrderLineId, int? PcmId, int Qty)[] lines)
    {
        var lay = new CuttingLay
        {
            WorkOrderId = woId, CuttingTeamId = S0.TCut01.TeamId, WorkDate = T, ShiftCode = shift, LaySequence = seq,
            Status = CuttingLayStatuses.Recorded, CreatedAt = Clock.Now,
        };
        Db.CuttingLays.Add(lay);
        await Db.SaveChangesAsync();
        Db.CuttingLayLines.AddRange(lines.Select(l => new CuttingLayLine { CuttingLayId = lay.CuttingLayId, OrderLineId = l.OrderLineId, ProductComponentMaterialId = l.PcmId, ActualQty = l.Qty }));
        Db.CuttingLayFabrics.Add(new CuttingLayFabric { CuttingLayId = lay.CuttingLayId, MaterialId = B.Ma1.MaterialId, UsedM = 5m });
        await Db.SaveChangesAsync();
        return lay;
    }

    /// <summary>Thêm phiếu bàn giao PENDING trực tiếp vào DB.</summary>
    protected async Task<(CutHandover Ch, List<CutHandoverLine> Lines)> AddHandoverAsync(int woId, params (int OrderLineId, int? PcmId, int Qty)[] lines)
    {
        var ch = new CutHandover { HandoverCode = "HB-B7-" + Guid.NewGuid().ToString("N")[..8], WorkOrderId = woId, HandedAt = Clock.Now, Status = HandoverStatuses.Pending };
        Db.CutHandovers.Add(ch);
        await Db.SaveChangesAsync();
        var ls = lines.Select(l => new CutHandoverLine { CutHandoverId = ch.CutHandoverId, OrderLineId = l.OrderLineId, ProductComponentMaterialId = l.PcmId, HandedQty = l.Qty }).ToList();
        Db.CutHandoverLines.AddRange(ls);
        await Db.SaveChangesAsync();
        return (ch, ls);
    }

    protected async Task<List<StockTransaction>> ChStAsync(int handoverId)
    {
        await using var db = NewDb();
        return await db.StockTransactions.AsNoTracking().Where(x => x.SourceDocType == StockDocTypes.CutHandover && x.SourceDocId == handoverId).ToListAsync();
    }

    protected async Task<long> MaxStAsync()
    {
        await using var db = NewDb();
        return await db.StockTransactions.AsNoTracking().Select(x => (long?)x.StockTransactionId).MaxAsync() ?? 0;
    }

    protected async Task<long> MaxAuditAsync()
    {
        await using var db = NewDb();
        return await db.AuditLogs.AsNoTracking().Select(x => (long?)x.AuditLogId).MaxAsync() ?? 0;
    }

    protected async Task<Request> RequestAsync(int id)
    {
        await using var db = NewDb();
        return await db.Requests.AsNoTracking().SingleAsync(x => x.RequestId == id);
    }

    protected async Task<int> MissingWoIdAsync() => (await MaxWoIdAsync()) + 1_000_000;

    protected async Task<int> MissingLayIdAsync() => (await MaxLayIdAsync()) + 1_000_000;

    protected async Task<int> MissingChIdAsync() => (await MaxChIdAsync()) + 1_000_000;

    // ------------------------------------------------------------------ body mẫu

    protected static object WL(int orderLineId, int qty, int? pcmId = null) =>
        new { OrderLineId = orderLineId, PlannedQty = qty, ProductComponentMaterialId = pcmId };

    protected Dictionary<string, object?> WoBody(object[]? lines = null, int? orderId = null, int? productId = null, int? teamId = null, DateOnly? due = null,
        string? note = "Cắt đợt 2", decimal tolerance = 2.50m) => new()
    {
        ["OrderId"] = orderId ?? K.Od2.OrderId, ["ProductId"] = productId ?? B.Pr1.ProductId, ["CuttingTeamId"] = teamId ?? K.TCut02.TeamId,
        ["DueDate"] = due ?? T.AddDays(7), ["Note"] = note, ["FabricTolerancePercent"] = tolerance,
        ["Lines"] = lines ?? [WL(K.Ol3.OrderLineId, 30), WL(K.Ol4.OrderLineId, 40)],
    };

    protected static object LF(int materialId, decimal? usedM) => new { MaterialId = materialId, UsedM = usedM };

    protected static object LL(int orderLineId, int qty, int? pcmId = null) => new { OrderLineId = orderLineId, ProductComponentMaterialId = pcmId, ActualQty = qty };

    protected Dictionary<string, object?> LayBody(int workOrderId, object[]? lines = null, object[]? fabrics = null, string shift = "AFTERNOON", DateOnly? date = null,
        string? note = "Bàn 1") => new()
    {
        ["WorkOrderId"] = workOrderId, ["WorkDate"] = date ?? T, ["ShiftCode"] = shift, ["Note"] = note,
        ["Fabrics"] = fabrics ?? [LF(B.Ma1.MaterialId, 45.50m)],
        ["Lines"] = lines ?? [LL(K.Ol3.OrderLineId, 60)],
    };

    protected static object HL(int orderLineId, int qty, int? pcmId = null) => new { OrderLineId = orderLineId, ProductComponentMaterialId = pcmId, HandedQty = qty };

    protected static object HandoverBody(int workOrderId, params object[] lines) => new { WorkOrderId = workOrderId, Lines = lines };

    protected static object CF(int lineId, int qty, string? reason = null) => new { CutHandoverLineId = lineId, ConfirmedQty = qty, DiscrepancyReason = reason };

    protected static object ConfirmBody(params object[] lines) => new { Lines = lines };

    protected static string Vn(int length) => string.Concat(Enumerable.Repeat("Ghi chú cắt ", length / 12 + 1))[..length];

    protected static int IdOf(Api r, string name) => B1Json.Int(r.Data, name) ?? -1;

    protected static void NoLeak(Api r) => r.Body.Should().NotContainAny(Leaks).And.NotContainAny("usp_Stock_Post", "RAISERROR");

    protected static void OkOrGraceful(Api r, params int[] failCodes)
    {
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf([200, .. failCodes], r.ToString());
        NoLeak(r);
    }

    protected static void NearVnNow(DateTime? value, string what)
    {
        value.Should().NotBeNull(what);
        var near = Math.Abs((value!.Value - DateTime.UtcNow.AddHours(7)).TotalHours) <= 2 || Math.Abs((value.Value - FixedClock.Default.Now).TotalHours) <= 2;
        near.Should().BeTrue($"{what}: {value:O} phải ≈ giờ VN hoặc FixedClock");
    }
}
