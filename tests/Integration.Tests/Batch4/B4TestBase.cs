using System.Text.Json;
using ClosedXML.Excel;
using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Batch2;
using Integration.Tests.Infrastructure;
using Integration.Tests.Seed;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch4;

public sealed record Snap4(DbSnapshot Counts, string Detail);

/// <summary>Nền chung IT_B4: S0 Batch 1 + B2 + S0 Batch 4 (<see cref="B4Context"/>, alias D). Mỗi test chạy trong ambient transaction và Rollback.</summary>
public abstract class B4TestBase : B2TestBase
{
    protected B4Context D { get; private set; } = null!;

    protected B4TestBase(ITestOutputHelper output) : base(output) { }

    protected override async Task OnSeededAsync(TestDataSeeder seeder)
    {
        await base.OnSeededAsync(seeder);
        D = await seeder.SeedBatch4BaseAsync(B, S0);
    }

    protected static readonly HttpMethod Get = HttpMethod.Get, Post = HttpMethod.Post, Put = HttpMethod.Put, Del = HttpMethod.Delete;
    protected static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    // ------------------------------------------------------------------ HTTP

    protected async Task<Api> Call(string? who, HttpMethod m, string url, object? body = null)
    {
        var (res, json) = await SendAsAsync(who, m, url, body);
        using (res)
            return new Api { Status = res.StatusCode, Body = await res.Content.ReadAsStringAsync(), Root = json ?? default, IsJson = json is not null };
    }

    /// <summary>Như <see cref="Call"/>, nhưng "qaqc" / "electrician" dùng JWT dựng sẵn (S0 không có tài khoản đăng nhập được cho hai vai trò này).</summary>
    protected async Task<Api> CallAs(string who, HttpMethod m, string url, object? body = null) => who switch
    {
        "qaqc" => await SendAsync(m, url, await TkQc(), body),
        "electrician" => await SendAsync(m, url, await TkElec(), body),
        _ => await Call(who, m, url, body),
    };

    protected Task<Api> Adm(HttpMethod m, string url, object? body = null) => Call("admin", m, url, body);
    protected Task<Api> Mgr(HttpMethod m, string url, object? body = null) => Call("manager", m, url, body);

    protected Task<string> TkElec() => CraftJwtAsync(S0.Lck, "access", DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(1));

    protected static void Created(Api r) => r.Code.Should().BeOneOf(new[] { 200, 201 }, r.ToString());

    protected static readonly string[] Leaks = ["UQ_", "UX_", "FK_", "CK_", "trg_", "SqlException", "Cannot insert duplicate", "StackTrace", "   at "];

    /// <summary>Lỗi phải êm: không 5xx, mã nằm trong tập cho phép, body không lộ tên constraint / trigger / SqlException.</summary>
    protected static void Rejected(Api r, params int[] codes)
    {
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(codes, r.ToString());
        r.Body.Should().NotContainAny(Leaks);
    }

    /// <summary>Gửi lần lượt các request bằng <paramref name="who"/>; mỗi request phải trả một trong <paramref name="codes"/> và DB không đổi.</summary>
    protected async Task DenyAsync(string? who, int[] codes, params (HttpMethod M, string Url)[] reqs)
    {
        var before = await Snap();
        foreach (var (m, url) in reqs)
        {
            var (res, _) = await SendAsAsync(who, m, url, m == HttpMethod.Get ? null : new { });
            using (res)
            {
                if (codes.Length == 1) await AssertErrorAsync(res, codes[0]);
                else ((int)res.StatusCode).Should().BeOneOf(codes, $"{m} {url}");
                ((int)res.StatusCode).Should().BeLessThan(500, $"{m} {url}");
            }
        }

        await Same(before);
    }

    // ------------------------------------------------------------------ snapshot DB

    protected async Task<Snap4> Snap()
    {
        var counts = await CaptureDbSnapshotAsync();
        var b2 = await SnapshotB2Async();
        await using var db = NewDb();
        var orders = await db.CustomerOrders.AsNoTracking().OrderBy(x => x.OrderId).Select(x => new
        {
            x.OrderId, x.OrderCode, x.Status, x.MaterialStatus, x.StopReason, x.FinalAmount, x.DelayRiskSince, x.DeliveryAddress, x.Note,
            x.CustomerDeliveryCount, x.CustomerDeliveryDate, x.CustomerDeliveryDateTo, x.UpdatedAt, x.RowVer, x.DueDate, x.ReceivedDate,
        }).ToListAsync();
        var lines = await db.OrderLines.AsNoTracking().OrderBy(x => x.OrderLineId).Select(x => new { x.OrderLineId, x.CmtUnitPrice }).ToListAsync();
        var teams = await db.OrderLineTeams.AsNoTracking().OrderBy(x => x.OrderLineTeamId).Select(x => new { x.OrderLineTeamId, x.OrderLineId, x.SewingTeamId }).ToListAsync();
        var dsd = await db.DeliveryScheduleDates.AsNoTracking().OrderBy(x => x.DeliveryScheduleDateId)
            .Select(x => new { x.DeliveryScheduleDateId, x.OrderId, x.SeqNo, x.DeliveryDate }).ToListAsync();
        var ratios = await db.ProductMarkerRatios.AsNoTracking().OrderBy(x => x.ProductMarkerId).ThenBy(x => x.SizeCode)
            .Select(x => new { x.ProductMarkerId, x.SizeCode, x.PiecesPerPly }).ToListAsync();
        var oms = await db.OrderMaterials.AsNoTracking().OrderBy(x => x.OrderMaterialId).Select(x => new { x.OrderMaterialId, x.Source, x.Note }).ToListAsync();
        return new Snap4(counts, b2 + JsonSerializer.Serialize(new { orders, lines, teams, dsd, ratios, oms }));
    }

    protected async Task Same(Snap4 before, string because = "")
    {
        await before.Counts.AssertUnchangedAsync(because);
        (await Snap()).Detail.Should().Be(before.Detail, $"[DB] không được thay đổi. {because}");
    }

    // ------------------------------------------------------------------ truy vấn DB

    protected async Task<CustomerOrder?> OrderAsync(int id)
    {
        await using var db = NewDb();
        return await db.CustomerOrders.AsNoTracking().SingleOrDefaultAsync(o => o.OrderId == id);
    }

    protected async Task<CustomerOrder?> OrderByCodeAsync(string code)
    {
        await using var db = NewDb();
        return await db.CustomerOrders.AsNoTracking().SingleOrDefaultAsync(o => o.OrderCode == code);
    }

    protected async Task<int> CountOrdersByCodeAsync(string code)
    {
        await using var db = NewDb();
        return await db.CustomerOrders.AsNoTracking().CountAsync(o => o.OrderCode == code);
    }

    protected async Task<byte[]> OrdRowVerAsync(int orderId) => (await OrderAsync(orderId))!.RowVer;

    protected async Task<List<OrderLine>> LinesAsync(int orderId)
    {
        await using var db = NewDb();
        return await db.OrderLines.AsNoTracking().Where(l => l.OrderId == orderId).OrderBy(l => l.OrderLineId).ToListAsync();
    }

    /// <summary>Màu / size của dòng đơn lấy qua join ProductVariant ↔ ProductColor (OrderLine không có cột Color / SizeCode).</summary>
    protected async Task<List<(int OrderLineId, int VariantId, string Color, string Size, int Qty)>> LineVariantsAsync(int orderId)
    {
        await using var db = NewDb();
        var rows = await (from l in db.OrderLines.AsNoTracking()
                          join v in db.ProductVariants.AsNoTracking() on l.ProductVariantId equals v.ProductVariantId
                          join c in db.ProductColors.AsNoTracking() on v.ProductColorId equals c.ProductColorId
                          where l.OrderId == orderId
                          orderby l.OrderLineId
                          select new { l.OrderLineId, l.ProductVariantId, c.Color, v.SizeCode, l.OrderedQty }).ToListAsync();
        return rows.Select(r => (r.OrderLineId, r.ProductVariantId, r.Color, r.SizeCode, r.OrderedQty)).ToList();
    }

    protected async Task<List<int>> TeamIdsAsync(int lineId)
    {
        await using var db = NewDb();
        return await db.OrderLineTeams.AsNoTracking().Where(t => t.OrderLineId == lineId).OrderBy(t => t.SewingTeamId).Select(t => t.SewingTeamId).ToListAsync();
    }

    protected async Task<List<OrderMaterial>> OmsAsync(int orderId)
    {
        await using var db = NewDb();
        return await db.OrderMaterials.AsNoTracking().Where(o => o.OrderId == orderId).OrderBy(o => o.OrderMaterialId).ToListAsync();
    }

    protected async Task<OrderMaterial?> OmAsync(int orderId, int materialId)
    {
        await using var db = NewDb();
        return await db.OrderMaterials.AsNoTracking().SingleOrDefaultAsync(o => o.OrderId == orderId && o.MaterialId == materialId);
    }

    protected async Task<List<DeliveryScheduleDate>> DsdAsync(int orderId)
    {
        await using var db = NewDb();
        return await db.DeliveryScheduleDates.AsNoTracking().Where(d => d.OrderId == orderId).OrderBy(d => d.SeqNo).ToListAsync();
    }

    protected async Task<List<ProductMarker>> MarkersAsync(int orderId, int productId)
    {
        await using var db = NewDb();
        return await db.ProductMarkers.AsNoTracking().Where(m => m.OrderId == orderId && m.ProductId == productId).OrderBy(m => m.MarkerNo).ThenBy(m => m.MarkerCode).ToListAsync();
    }

    protected async Task<List<ProductMarkerRatio>> RatiosAsync(int markerId)
    {
        await using var db = NewDb();
        return await db.ProductMarkerRatios.AsNoTracking().Where(r => r.ProductMarkerId == markerId).OrderBy(r => r.SizeCode).ToListAsync();
    }

    protected async Task<int> CountProductMarkersAsync()
    {
        await using var db = NewDb();
        return await db.ProductMarkers.AsNoTracking().CountAsync();
    }

    protected async Task<int> DeliveredQtyAsync(int orderLineId)
    {
        await using var db = NewDb();
        var rows = await db.Database.SqlQuery<int>($"SELECT CAST(DeliveredQty AS int) AS [Value] FROM dbo.vw_OrderLineProgress WHERE OrderLineId = {orderLineId}").ToListAsync();
        return rows.SingleOrDefault();
    }

    protected async Task<int> DispatchSumAsync(int orderLineId, string status)
    {
        await using var db = NewDb();
        return await (from n in db.DispatchNotes.AsNoTracking()
                      join l in db.DispatchNoteLines.AsNoTracking() on n.DispatchNoteId equals l.DispatchNoteId
                      where l.OrderLineId == orderLineId && n.Status == status
                      select l.IssueQty).SumAsync();
    }

    protected async Task<int> MissingOrderLineIdAsync()
    {
        await using var db = NewDb();
        return (await db.OrderLines.AsNoTracking().MaxAsync(l => l.OrderLineId)) + 1_000_000;
    }

    protected async Task<int> MissingOrderMaterialIdAsync()
    {
        await using var db = NewDb();
        return (await db.OrderMaterials.AsNoTracking().MaxAsync(l => (int?)l.OrderMaterialId) ?? 0) + 1_000_000;
    }

    // ------------------------------------------------------------------ body mẫu

    protected static string Code(string prefix = "DH-IT") => prefix + Guid.NewGuid().ToString("N")[..8];

    protected static object Ln(int productId, string color, string size, int qty, decimal price = 25_000m, int? lineId = null) =>
        new { OrderLineId = lineId, ProductId = productId, Color = color, SizeCode = size, OrderedQty = qty, CmtUnitPrice = price };

    protected Dictionary<string, object?> OrderBody(string code, object[]? lines = null, int? customerId = null) => new()
    {
        ["OrderCode"] = code, ["CustomerId"] = customerId ?? B.Cu1.CustomerId, ["ReceivedDate"] = Today, ["DueDate"] = Today.AddDays(30),
        ["DeliveryAddress"] = "KCN Tân Tạo", ["Note"] = null,
        ["Lines"] = lines ?? [Ln(B.Pr1.ProductId, "Đen", "M", 100)],
    };

    /// <summary>Body PUT đơn: giữ nguyên thông tin đơn trong DB, ghi đè Lines (và RowVer nếu truyền).</summary>
    protected async Task<Dictionary<string, object?>> UpdateBodyAsync(CustomerOrder od, object[] lines, byte[]? rowVer = null, int? customerId = null)
    {
        var cur = (await OrderAsync(od.OrderId))!;
        return new()
        {
            ["OrderCode"] = cur.OrderCode, ["CustomerId"] = customerId ?? cur.CustomerId, ["ReceivedDate"] = cur.ReceivedDate, ["DueDate"] = cur.DueDate,
            ["DeliveryAddress"] = cur.DeliveryAddress, ["Note"] = cur.Note, ["Lines"] = lines, ["RowVer"] = rowVer ?? cur.RowVer,
        };
    }

    /// <summary>Hai dòng hiện có của OD1 (OL1, OL2), giữ nguyên.</summary>
    protected object[] KeepOd1() =>
    [
        Ln(B.Pr1.ProductId, "Đen", "M", 100, 25_000m, D.Ol1.OrderLineId), Ln(B.Pr1.ProductId, "Đen", "L", 50, 25_000m, D.Ol2.OrderLineId),
    ];

    protected static int OrderIdOf(Api r) => B1Json.Int(r.Data, "orderId") ?? -1;

    // ------------------------------------------------------------------ marker / NVL body

    protected static object MarkerIn(string code, int no, decimal len, decimal? wid, string status = "ACTIVE", object[]? ratios = null, int? id = null) => new
    {
        ProductMarkerId = id, MarkerNo = no, MarkerCode = code, LengthM = len, WidthM = wid, Status = status,
        Ratios = ratios ?? [new { SizeCode = "M", PiecesPerPly = 2 }, new { SizeCode = "L", PiecesPerPly = 1 }],
    };

    protected static object RatioIn(string size, int ppp) => new { SizeCode = size, PiecesPerPly = ppp };

    protected static object SaveMarkers(params object[] markers) => new { Markers = markers };

    protected string MarkersUrl(CustomerOrder od, int? productId = null) => $"{M}order-markers/{od.OrderId}/products/{productId ?? B.Pr1.ProductId}";

    protected static object OmBody(int orderId, int materialId, decimal required, decimal customer, string? note = null) =>
        new { OrderId = orderId, MaterialId = materialId, RequiredQty = required, CustomerQty = customer, Note = note };

    protected static object OmLine(int materialId, decimal required, decimal customer, string? note = null) =>
        new { MaterialId = materialId, RequiredQty = required, CustomerQty = customer, Note = note };

    protected static object OmRow(int row, string code, decimal? required, decimal? customer = 0m, string? name = null, string? unit = null, string? err = null) =>
        new { RowNumber = row, MaterialCode = code, RequiredQty = required, CustomerQty = customer, Note = (string?)null, ParseError = err, MaterialName = name, Unit = unit };

    // ------------------------------------------------------------------ Excel

    private static string Norm(string? s) => (s ?? "").Normalize().Trim().ToLowerInvariant();

    /// <summary>
    /// Mở file mẫu, tìm dòng tiêu đề có đủ các cột <paramref name="mustHave"/> ở sheet có tên bắt đầu bằng <paramref name="sheetPrefix"/>,
    /// xóa các dòng phía dưới rồi ghi <paramref name="rows"/> (khóa = đoạn tên cột). Không tìm thấy tiêu đề → ném lỗi kèm các dòng đầu để chỉnh seed.
    /// </summary>
    protected static byte[] FillSheet(byte[] template, string? sheetPrefix, string[] mustHave, IEnumerable<Dictionary<string, object?>> rows)
    {
        using var input = new MemoryStream(template);
        using var wb = new XLWorkbook(input);
        var ws = sheetPrefix is null ? wb.Worksheets.First() : wb.Worksheets.First(s => Norm(s.Name).StartsWith(Norm(sheetPrefix)));
        var last = ws.LastRowUsed()?.RowNumber() ?? 1;
        IXLRow? header = null;
        for (var r = 1; r <= Math.Min(last, 12) && header is null; r++)
        {
            var texts = ws.Row(r).CellsUsed().Select(c => Norm(c.GetString())).ToList();
            if (mustHave.All(k => texts.Any(t => t.Contains(Norm(k))))) header = ws.Row(r);
        }

        if (header is null)
        {
            var dump = string.Join(" // ", Enumerable.Range(1, Math.Min(last, 6)).Select(r => $"R{r}: " + string.Join(" | ", ws.Row(r).CellsUsed().Select(c => c.GetString()))));
            throw new InvalidOperationException($"seed: file mẫu '{ws.Name}' không có dòng tiêu đề chứa [{string.Join(", ", mustHave)}]. {dump}");
        }

        var cols = header.CellsUsed().ToDictionary(c => c.Address.ColumnNumber, c => Norm(c.GetString()));
        for (var r = last; r > header.RowNumber(); r--) ws.Row(r).Delete();
        var rowNo = header.RowNumber() + 1;
        foreach (var data in rows)
        {
            foreach (var (key, value) in data)
            {
                var k = Norm(key);
                var col = cols.Where(c => c.Value == k).Select(c => (int?)c.Key).FirstOrDefault()
                          ?? cols.Where(c => c.Value.Contains(k)).Select(c => (int?)c.Key).FirstOrDefault();
                if (col is null) continue;
                var cell = ws.Cell(rowNo, col.Value);
                cell.Style.Font.FontColor = XLColor.Black;
                switch (value)
                {
                    case null: break;
                    case decimal d: cell.Value = d; break;
                    case int i: cell.Value = i; break;
                    default: cell.Value = value.ToString(); break;
                }
            }

            rowNo++;
        }

        using var output = new MemoryStream();
        wb.SaveAs(output);
        return output.ToArray();
    }

    protected async Task<byte[]> TemplateOf(string kind, int? productId = null, int? orderId = null) =>
        await TemplateAsync(kind, productId, await TkAdm(), orderId);

    /// <summary>om_ok.xlsx: các dòng NVL của đơn (mã NVL, số cần, khách cấp). om_err.xlsx = một dòng có số cần âm.</summary>
    protected async Task<byte[]> OmFileAsync(int orderId, params (string Code, decimal Required, decimal Customer)[] rows) =>
        FillSheet(await TemplateOf("order-materials", null, orderId), null, ["Mã NVL", "Số cần"],
            rows.Select(r => new Dictionary<string, object?> { ["Mã NVL"] = r.Code, ["Số cần"] = r.Required, ["Khách cấp"] = r.Customer }));

    protected async Task<byte[]> OrderFileAsync(params (string ProductCode, string Color, decimal Price, int M, int L)[] rows) =>
        FillSheet(await TemplateOf("order-lines"), null, ["Mã hàng", "Màu"],
            rows.Select(r => new Dictionary<string, object?>
                { ["Mã hàng"] = r.ProductCode, ["Màu"] = r.Color, ["Đơn giá"] = r.Price, ["M"] = r.M, ["L"] = r.L }));

    /// <summary>marker_ok.xlsx / marker_w4.xlsx: sheet "Bìa của đơn" của file mẫu định mức, ghi các bìa (mã bìa, dài, khổ, số SP mỗi size).</summary>
    protected async Task<byte[]> MarkerFileAsync(CustomerOrder od, params (int No, string Code, decimal Len, decimal Wid)[] rows) =>
        FillSheet(await TemplateOf("spec", B.Pr1.ProductId, od.OrderId), "Bìa của đơn", ["Mã bìa", "Dài"],
            rows.Select(r => new Dictionary<string, object?>
                { ["Nhóm"] = r.No, ["Mã bìa"] = r.Code, ["Dài"] = r.Len, ["Khổ"] = r.Wid, ["M"] = 2, ["L"] = 1 }));

    protected static byte[] ZeroXlsx() => [];
}

