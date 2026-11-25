using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Batch4;
using Integration.Tests.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;
using Xunit.Abstractions;

namespace Integration.Tests.Batch7;

/// <summary>Nền cho ca đồng thời / fault-injection IT_B7: S0 (Batch 1 + B2 + B4 + B7) được COMMIT, request song song dùng connection độc lập, tự dọn sau test (kể cả thẻ kho append-only).</summary>
public abstract class B7ConcurrentTestBase : B4ConcurrentTestBase
{
    protected const string TLP = "/api/v1/team-leader/";
    private long _reqMark;
    private static int _seq = 100;

    protected B7Context K { get; private set; } = null!;

    protected B7ConcurrentTestBase(ITestOutputHelper output) : base(output) { }

    protected override string[] ExtraRootTables =>
        ["CutHandoverLine", "CutHandover", "CuttingLayFabric", "CuttingLayLine", "CuttingLay", "WorkOrderLine", "WorkOrder", "MaterialIssueLine", "MaterialIssue"];

    protected override async Task OnSeededAsync(TestDataSeeder seeder, NxMtdDbContext seedDb)
    {
        _reqMark = await seedDb.Requests.AsNoTracking().MaxAsync(x => (int?)x.RequestId) ?? 0;
        await base.OnSeededAsync(seeder, seedDb);
        K = await seeder.SeedBatch7BaseAsync(B, D, S0);
    }

    protected override async Task BeforeCleanupAsync(SqlConnection conn)
    {
        async Task Exec(string sql)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@r", _reqMark);
            await cmd.ExecuteNonQueryAsync();
        }

        await Exec("DISABLE TRIGGER dbo.trg_StockTransaction_AppendOnly ON dbo.StockTransaction");
        await Exec("UPDATE dbo.WorkOrder SET SourceRequestId = NULL WHERE SourceRequestId > @r");
        await Exec("DELETE FROM dbo.RequestLine WHERE RequestId > @r");
        await Exec("DELETE FROM dbo.Request WHERE RequestId > @r");
    }

    protected override async Task AfterCleanupAsync(SqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "ENABLE TRIGGER dbo.trg_StockTransaction_AppendOnly ON dbo.StockTransaction";
        await cmd.ExecuteNonQueryAsync();
    }

    protected static readonly DateOnly T = Integration.Tests.Infrastructure.FixedClock.Default.Today;
    protected static string WOM => M + "work-orders";
    protected static string CLT => TLP + "cutting-lays";
    protected static string CHT => TLP + "cut-handovers";
    protected static string CHW => WK + "cut-handovers";

    protected Task<string> TkTl1() => TokenAsync(S0.Tl2);

    // ------------------------------------------------------------------ dữ liệu riêng từng vòng

    /// <summary>Đơn đang sản xuất mới: L3 (Đen M, 200), L4 (Đen L, 100) của PR1 + bìa S1.</summary>
    protected async Task<(CustomerOrder Order, OrderLine L3, OrderLine L4)> NewOdAsync()
    {
        await using var db = NewDb();
        var od = new CustomerOrder
        {
            OrderCode = "DH-C7-" + Guid.NewGuid().ToString("N")[..10], CustomerId = B.Cu1.CustomerId, ReceivedDate = T.AddDays(-5), DueDate = T.AddDays(30),
            Status = OrderStatuses.InProduction, MaterialStatus = OrderMaterialStatuses.Partial, DeliveryAddress = "KCN Tân Tạo", CreatedAt = Clock.Now,
        };
        db.CustomerOrders.Add(od);
        await db.SaveChangesAsync();
        var l3 = new OrderLine { OrderId = od.OrderId, ProductVariantId = B.Pr1VarM.ProductVariantId, ProductId = B.Pr1.ProductId, OrderedQty = 200, CmtUnitPrice = 25_000m };
        var l4 = new OrderLine { OrderId = od.OrderId, ProductVariantId = B.Pr1VarL.ProductVariantId, ProductId = B.Pr1.ProductId, OrderedQty = 100, CmtUnitPrice = 25_000m };
        db.OrderLines.AddRange(l3, l4);
        await db.SaveChangesAsync();
        var issue = new MaterialIssue
        {
            IssueCode = "PC-C7-" + Guid.NewGuid().ToString("N")[..10], WarehouseType = WarehouseTypes.Material, OrderId = od.OrderId, ReceivingTeamId = S0.TCut01.TeamId,
            IssueDate = T, IssuedBy = S0.Wh.EmployeeId, Status = IssueStatuses.Received, ReceivedBy = S0.Tl2.EmployeeId, ReceivedAt = Clock.Now, CreatedAt = Clock.Now,
        };
        db.MaterialIssues.Add(issue);
        await db.SaveChangesAsync();
        db.MaterialIssueLines.Add(new MaterialIssueLine { MaterialIssueId = issue.MaterialIssueId, MaterialId = B.Ma1.MaterialId, IssuedQty = 1000m, ConfirmedQty = 1000m });
        await db.SaveChangesAsync();
        return (od, l3, l4);
    }

    /// <summary>Lệnh cắt mới của T_CUT01 trên đơn mới: WOL(L3, <paramref name="plan"/>), trạng thái <paramref name="status"/>.</summary>
    protected async Task<(WorkOrder Wo, OrderLine L3, OrderLine L4)> NewWoAsync(string status = WorkOrderStatuses.Open, int plan = 100)
    {
        var (od, l3, l4) = await NewOdAsync();
        await using var db = NewDb();
        var wo = new WorkOrder
        {
            WorkOrderCode = "LC-C7-" + Guid.NewGuid().ToString("N")[..10], OrderId = od.OrderId, ProductId = B.Pr1.ProductId, CuttingTeamId = S0.TCut01.TeamId,
            DueDate = T.AddDays(10), Status = status, CreatedAt = Clock.Now,
        };
        db.WorkOrders.Add(wo);
        await db.SaveChangesAsync();
        db.WorkOrderLines.Add(new WorkOrderLine { WorkOrderId = wo.WorkOrderId, OrderLineId = l3.OrderLineId, PlannedQty = plan });
        await db.SaveChangesAsync();
        return (wo, l3, l4);
    }

    /// <summary>Lệnh CUTTING đã cắt <paramref name="cut"/>, kèm phiếu PENDING giao <paramref name="handed"/> (mỗi dòng đúng 1 NVL vải Ma1).</summary>
    protected async Task<(WorkOrder Wo, OrderLine L3, CutHandover Ch, CutHandoverLine Chl)> NewPendingAsync(int plan = 50, int cut = 40, int handed = 30)
    {
        var (wo, l3, _) = await NewWoAsync(WorkOrderStatuses.Cutting, plan);
        await using var db = NewDb();
        var lay = new CuttingLay
        {
            WorkOrderId = wo.WorkOrderId, CuttingTeamId = S0.TCut01.TeamId, WorkDate = T, ShiftCode = "MORNING", LaySequence = (short)Interlocked.Increment(ref _seq),
            Status = CuttingLayStatuses.Recorded, CreatedAt = Clock.Now,
        };
        db.CuttingLays.Add(lay);
        await db.SaveChangesAsync();
        db.CuttingLayLines.Add(new CuttingLayLine { CuttingLayId = lay.CuttingLayId, OrderLineId = l3.OrderLineId, ActualQty = cut });
        db.CuttingLayFabrics.Add(new CuttingLayFabric { CuttingLayId = lay.CuttingLayId, MaterialId = B.Ma1.MaterialId, UsedM = 10m });
        var ch = new CutHandover { HandoverCode = "HB-C7-" + Guid.NewGuid().ToString("N")[..10], WorkOrderId = wo.WorkOrderId, HandedAt = Clock.Now, Status = HandoverStatuses.Pending };
        db.CutHandovers.Add(ch);
        await db.SaveChangesAsync();
        var chl = new CutHandoverLine { CutHandoverId = ch.CutHandoverId, OrderLineId = l3.OrderLineId, HandedQty = handed };
        db.CutHandoverLines.Add(chl);
        await db.SaveChangesAsync();
        return (wo, l3, ch, chl);
    }

    // ------------------------------------------------------------------ truy vấn DB

    protected async Task<WorkOrder> WoAsync(int id)
    {
        await using var db = NewDb();
        return await db.WorkOrders.AsNoTracking().SingleAsync(x => x.WorkOrderId == id);
    }

    protected async Task<List<WorkOrder>> WoOfOrderAsync(int orderId)
    {
        await using var db = NewDb();
        return await db.WorkOrders.AsNoTracking().Where(x => x.OrderId == orderId).OrderBy(x => x.WorkOrderId).ToListAsync();
    }

    protected async Task<List<WorkOrderLine>> WolsAsync(int woId)
    {
        await using var db = NewDb();
        return await db.WorkOrderLines.AsNoTracking().Where(x => x.WorkOrderId == woId).OrderBy(x => x.WorkOrderLineId).ToListAsync();
    }

    protected async Task<int> PlanSumOfOrderAsync(int orderId, int orderLineId)
    {
        await using var db = NewDb();
        return await (from l in db.WorkOrderLines.AsNoTracking()
                      join w in db.WorkOrders.AsNoTracking() on l.WorkOrderId equals w.WorkOrderId
                      where w.OrderId == orderId && l.OrderLineId == orderLineId && l.ProductComponentMaterialId == null && w.Status != WorkOrderStatuses.Cancelled
                      select l.PlannedQty).SumAsync();
    }

    protected async Task<List<CuttingLay>> LaysAsync(int woId)
    {
        await using var db = NewDb();
        return await db.CuttingLays.AsNoTracking().Where(x => x.WorkOrderId == woId).OrderBy(x => x.CuttingLayId).ToListAsync();
    }

    protected async Task<List<CuttingLayLine>> LayLinesAsync(int layId)
    {
        await using var db = NewDb();
        return await db.CuttingLayLines.AsNoTracking().Where(x => x.CuttingLayId == layId).OrderBy(x => x.CuttingLayLineId).ToListAsync();
    }

    protected async Task<int> OrphanCountAsync()
    {
        await using var db = NewDb();
        var a = await db.CuttingLayLines.AsNoTracking().CountAsync(x => !db.CuttingLays.Any(c => c.CuttingLayId == x.CuttingLayId));
        var b = await db.CuttingLayFabrics.AsNoTracking().CountAsync(x => !db.CuttingLays.Any(c => c.CuttingLayId == x.CuttingLayId));
        var c2 = await db.WorkOrderLines.AsNoTracking().CountAsync(x => !db.WorkOrders.Any(w => w.WorkOrderId == x.WorkOrderId));
        var d = await db.CutHandoverLines.AsNoTracking().CountAsync(x => !db.CutHandovers.Any(h => h.CutHandoverId == x.CutHandoverId));
        return a + b + c2 + d;
    }

    protected async Task<CutHandover> ChAsync(int id)
    {
        await using var db = NewDb();
        return await db.CutHandovers.AsNoTracking().SingleAsync(x => x.CutHandoverId == id);
    }

    protected async Task<List<CutHandover>> ChOfWoAsync(int woId)
    {
        await using var db = NewDb();
        return await db.CutHandovers.AsNoTracking().Where(x => x.WorkOrderId == woId).OrderBy(x => x.CutHandoverId).ToListAsync();
    }

    protected async Task<int> HandedSumAsync(int woId, int orderLineId)
    {
        await using var db = NewDb();
        return await (from l in db.CutHandoverLines.AsNoTracking()
                      join h in db.CutHandovers.AsNoTracking() on l.CutHandoverId equals h.CutHandoverId
                      where h.WorkOrderId == woId && l.OrderLineId == orderLineId && (h.Status == HandoverStatuses.Pending || h.Status == HandoverStatuses.Confirmed)
                      select l.HandedQty).SumAsync();
    }

    protected async Task<int> CutSumAsync(int woId, int orderLineId)
    {
        await using var db = NewDb();
        return await (from l in db.CuttingLayLines.AsNoTracking()
                      join c in db.CuttingLays.AsNoTracking() on l.CuttingLayId equals c.CuttingLayId
                      where c.WorkOrderId == woId && l.OrderLineId == orderLineId
                      select l.ActualQty).SumAsync();
    }

    protected async Task<List<CutHandoverLine>> ChlsAsync(int handoverId)
    {
        await using var db = NewDb();
        return await db.CutHandoverLines.AsNoTracking().Where(x => x.CutHandoverId == handoverId).OrderBy(x => x.CutHandoverLineId).ToListAsync();
    }

    protected async Task<decimal> BtpOnHandAsync(int orderLineId)
    {
        await using var db = NewDb();
        return await db.StockBalances.AsNoTracking()
            .Where(x => x.WarehouseType == WarehouseTypes.SemiFinished && x.OrderLineId == orderLineId)
            .Select(x => (decimal?)x.OnHandQty).SumAsync() ?? 0m;
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

    // ------------------------------------------------------------------ body

    protected static object WL(int orderLineId, int qty, int? pcmId = null) =>
        new { OrderLineId = orderLineId, PlannedQty = qty, ProductComponentMaterialId = pcmId };

    protected Dictionary<string, object?> WoBody(int orderId, object[] lines, int? teamId = null) => new()
    {
        ["OrderId"] = orderId, ["ProductId"] = B.Pr1.ProductId, ["CuttingTeamId"] = teamId ?? S0.TCut01.TeamId, ["DueDate"] = T.AddDays(7),
        ["Note"] = null, ["FabricTolerancePercent"] = 0m, ["Lines"] = lines,
    };

    protected Dictionary<string, object?> LayBody(int woId, int orderLineId, int qty, string shift = "OVERTIME", decimal usedM = 5m) => new()
    {
        ["WorkOrderId"] = woId, ["WorkDate"] = T, ["ShiftCode"] = shift, ["Note"] = null,
        ["Fabrics"] = new[] { new { MaterialId = B.Ma1.MaterialId, UsedM = (decimal?)usedM } },
        ["Lines"] = new[] { new { OrderLineId = orderLineId, ProductComponentMaterialId = (int?)null, ActualQty = qty } },
    };

    protected static object HandoverBody(int woId, int orderLineId, int qty) =>
        new { WorkOrderId = woId, Lines = new[] { new { OrderLineId = orderLineId, ProductComponentMaterialId = (int?)null, HandedQty = qty } } };

    protected static object ConfirmBody(int lineId, int qty) =>
        new { Lines = new[] { new { CutHandoverLineId = lineId, ConfirmedQty = qty, DiscrepancyReason = (string?)null } } };

    protected static int IdOf(Api r, string name) => Integration.Tests.Batch1.B1Json.Int(r.Data, name) ?? -1;

    // ------------------------------------------------------------------ fault-injection (trigger test-only, luôn gỡ sau khi dùng)

    /// <summary><paramref name="when"/> = điều kiện T-SQL (null = luôn ném lỗi) đặt trước THROW trong trigger AFTER INSERT, UPDATE.</summary>
    protected async Task WithTriggerAsync(string table, string name, Func<Task> act, string? when = null)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        async Task Exec(string sql)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        await Exec($"IF OBJECT_ID('dbo.{name}','TR') IS NOT NULL DROP TRIGGER dbo.{name}");
        await Exec($"CREATE TRIGGER dbo.{name} ON dbo.{table} AFTER INSERT, UPDATE AS BEGIN {(when is null ? "" : $"IF NOT ({when}) RETURN;")} THROW 51000, 'it-b7 injected fault', 1; END");
        try { await act(); }
        finally { await Exec($"IF OBJECT_ID('dbo.{name}','TR') IS NOT NULL DROP TRIGGER dbo.{name}"); }
    }

    protected static void FailedGracefully(Api r)
    {
        r.Code.Should().BeInRange(400, 499, "lỗi DB phải khiến request thất bại có kiểm soát (4xx), không báo thành công, không 5xx. " + r);
        r.Body.Should().NotContainAny("SqlException", "StackTrace", "   at ", "it-b7 injected fault", "trg_it_b7");
    }
}
