using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Batch2;
using Integration.Tests.Seed;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

/// <summary>Nền cho ca đồng thời IT_B4: S0 (Batch 1 + B2 + B4) được COMMIT, request song song dùng connection độc lập, tự dọn sau test.</summary>
public abstract class B4ConcurrentTestBase : B2ConcurrentTestBase
{
    protected B4Context D { get; private set; } = null!;

    protected B4ConcurrentTestBase(ITestOutputHelper output) : base(output) { }

    protected override async Task OnSeededAsync(TestDataSeeder seeder, NxMtdDbContext seedDb) => D = await seeder.SeedBatch4BaseAsync(B, S0);

    protected static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    protected Task<string> TkMgr() => TokenAsync(S0.Mgr);

    protected async Task<string> TkMgrOrAdm(bool manager) => manager ? await TkMgr() : await TkAdm();

    protected static string Code(string prefix = "DH-IT") => prefix + Guid.NewGuid().ToString("N")[..8];

    protected static object Ln(int productId, string color, string size, int qty, decimal price = 25_000m, int? lineId = null) =>
        new { OrderLineId = lineId, ProductId = productId, Color = color, SizeCode = size, OrderedQty = qty, CmtUnitPrice = price };

    protected object OrderBody(string code, int lines = 2) => new Dictionary<string, object?>
    {
        ["OrderCode"] = code, ["CustomerId"] = B.Cu1.CustomerId, ["ReceivedDate"] = Today, ["DueDate"] = Today.AddDays(30),
        ["DeliveryAddress"] = "KCN Tân Tạo", ["Note"] = null,
        ["Lines"] = lines == 2
            ? new[] { Ln(B.Pr1.ProductId, "Đen", "M", 100), Ln(B.Pr1.ProductId, "Đen", "L", 50) }
            : new[] { Ln(B.Pr1.ProductId, "Đen", "M", 100) },
    };

    protected async Task<Dictionary<string, object?>> UpdateBodyAsync(CustomerOrder od, object[] lines, DateOnly due, byte[]? rowVer = null)
    {
        await using var db = NewDb();
        var cur = await db.CustomerOrders.AsNoTracking().SingleAsync(o => o.OrderId == od.OrderId);
        return new()
        {
            ["OrderCode"] = cur.OrderCode, ["CustomerId"] = cur.CustomerId, ["ReceivedDate"] = cur.ReceivedDate, ["DueDate"] = due,
            ["DeliveryAddress"] = cur.DeliveryAddress, ["Note"] = cur.Note, ["Lines"] = lines, ["RowVer"] = rowVer ?? cur.RowVer,
        };
    }

    protected async Task<CustomerOrder> OrderAsync(int id)
    {
        await using var db = NewDb();
        return await db.CustomerOrders.AsNoTracking().SingleAsync(o => o.OrderId == id);
    }

    protected async Task<int> CountOrdersByCodeAsync(string code)
    {
        await using var db = NewDb();
        return await db.CustomerOrders.AsNoTracking().CountAsync(o => o.OrderCode == code);
    }

    protected async Task<List<OrderLine>> LinesAsync(int orderId)
    {
        await using var db = NewDb();
        return await db.OrderLines.AsNoTracking().Where(l => l.OrderId == orderId).ToListAsync();
    }

    protected async Task<List<int>> TeamIdsAsync(int lineId)
    {
        await using var db = NewDb();
        return await db.OrderLineTeams.AsNoTracking().Where(t => t.OrderLineId == lineId).OrderBy(t => t.SewingTeamId).Select(t => t.SewingTeamId).ToListAsync();
    }

    protected async Task<int> CountOmAsync(int orderId, int materialId)
    {
        await using var db = NewDb();
        return await db.OrderMaterials.AsNoTracking().CountAsync(o => o.OrderId == orderId && o.MaterialId == materialId);
    }

    protected async Task<List<DeliveryScheduleDate>> DsdAsync(int orderId)
    {
        await using var db = NewDb();
        return await db.DeliveryScheduleDates.AsNoTracking().Where(d => d.OrderId == orderId).OrderBy(d => d.SeqNo).ToListAsync();
    }

    protected async Task<List<ProductMarker>> MarkersAsync(int orderId, int productId)
    {
        await using var db = NewDb();
        return await db.ProductMarkers.AsNoTracking().Where(m => m.OrderId == orderId && m.ProductId == productId).OrderBy(m => m.MarkerNo).ToListAsync();
    }

    protected async Task<List<ProductMarkerRatio>> RatiosAsync(int markerId)
    {
        await using var db = NewDb();
        return await db.ProductMarkerRatios.AsNoTracking().Where(r => r.ProductMarkerId == markerId).OrderBy(r => r.SizeCode).ToListAsync();
    }

    protected static object MarkerIn(string code, int no, decimal len, decimal wid) => new
    {
        ProductMarkerId = (int?)null, MarkerNo = no, MarkerCode = code, LengthM = len, WidthM = (decimal?)wid, Status = "ACTIVE",
        Ratios = new[] { new { SizeCode = "M", PiecesPerPly = 2 }, new { SizeCode = "L", PiecesPerPly = 1 } },
    };

    /// <summary>Đơn mới (NEW) trong DB cho từng vòng lặp: 2 dòng (Đen M, Đen L) của PR1; <paramref name="ready"/> = đã có NVL (MaterialStatus PARTIAL).</summary>
    protected async Task<(CustomerOrder Order, List<OrderLine> Lines)> NewOrderAsync(bool ready = false)
    {
        await using var db = NewDb();
        var od = new CustomerOrder
        {
            OrderCode = Code(), CustomerId = B.Cu1.CustomerId, ReceivedDate = Today.AddDays(-5), DueDate = Today.AddDays(30), Status = "NEW",
            MaterialStatus = ready ? "PARTIAL" : "AWAITING", DeliveryAddress = "KCN Tân Tạo", CreatedAt = Clock.Now,
        };
        db.CustomerOrders.Add(od);
        await db.SaveChangesAsync();
        var lines = new List<OrderLine>();
        foreach (var v in new[] { B.Pr1VarM, B.Pr1VarL })
            lines.Add(new OrderLine { OrderId = od.OrderId, ProductVariantId = v.ProductVariantId, ProductId = B.Pr1.ProductId, OrderedQty = 100, CmtUnitPrice = 25_000m });
        db.OrderLines.AddRange(lines);
        await db.SaveChangesAsync();
        return (od, lines);
    }

    /// <summary>Số vòng lặp bên trong mỗi ca đồng thời (mỗi vòng dùng dữ liệu riêng, assert ngay).</summary>
    protected const int Rounds = 6;

    protected static void OneWinner(Api[] results, params int[] loserCodes)
    {
        AssertRaceNoServerError(results);
        results.Count(IsSuccess).Should().Be(1, "đúng 1 request thắng. " + Summary(results));
        results.Where(r => !IsSuccess(r)).Should().OnlyContain(r => loserCodes.Contains(r.Code), Summary(results));
    }

    protected static void AllowedOutcomes(Api[] results)
    {
        AssertRaceNoServerError(results);
        results.Should().OnlyContain(r => new[] { 200, 201, 400, 409 }.Contains(r.Code), Summary(results));
        results.Count(IsSuccess).Should().BeGreaterThanOrEqualTo(1, Summary(results));
    }
}

