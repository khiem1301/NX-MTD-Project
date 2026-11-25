using System.Text.Json;
using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Batch2;
using Integration.Tests.Seed;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>Nền chung IT_B3: S0 Batch 1 + B2 (PR1, PR2, OD1…) + S0 Batch 3 (<see cref="B3Context"/>). Mỗi test chạy trong ambient transaction và Rollback.</summary>
public abstract class B3TestBase : B2TestBase
{
    protected B3Context P { get; private set; } = null!;

    protected B3TestBase(ITestOutputHelper output) : base(output) { }

    protected override async Task OnSeededAsync(TestDataSeeder seeder)
    {
        await base.OnSeededAsync(seeder);
        P = await seeder.SeedBatch3BaseAsync(B);
    }

    protected static readonly DateOnly D1201 = new(2026, 12, 1);

    // ------------------------------------------------------------------ HTTP

    protected Task<(HttpResponseMessage Response, JsonElement? Json)> Send(string? who, HttpMethod m, string url, object? body = null) =>
        SendAsAsync(who, m, url, body);

    /// <summary>Gọi bằng vai trò S0, trả về <see cref="Api"/> (status + JSON) để assert dữ liệu trả về.</summary>
    protected async Task<Api> Call(string who, HttpMethod m, string url, object? body = null)
    {
        var (res, json) = await Send(who, m, url, body);
        using (res)
        {
            return new Api
            {
                Status = res.StatusCode, Body = await res.Content.ReadAsStringAsync(), Root = json ?? default, IsJson = json is not null,
            };
        }
    }

    protected Task<Api> Adm(HttpMethod m, string url, object? body = null) => Call("admin", m, url, body);
    protected Task<Api> Acc(HttpMethod m, string url, object? body = null) => Call("accountant", m, url, body);

    protected static void Created(Api r) => r.Code.Should().BeOneOf(new[] { 200, 201 }, r.ToString());

    protected static void Failed(Api r, params int[] codes)
    {
        r.ShouldNotBeServerError();
        r.Code.Should().BeOneOf(codes, r.ToString());
        r.Body.Should().NotContainAny("UQ_", "UX_", "FK_", "trg_", "SqlException", "Cannot insert duplicate", "StackTrace");
    }

    /// <summary>Thay {PR1}, {OP2}, {PD1}… bằng id động của S0 trong URL mẫu.</summary>
    protected string R(string s) => s
        .Replace("{PR1}", B.Pr1.ProductId.ToString()).Replace("{PR2}", B.Pr2.ProductId.ToString()).Replace("{PR3}", B.Pr3.ProductId.ToString())
        .Replace("{OD1}", B.Od1.OrderId.ToString()).Replace("{OD2}", P.Od2.OrderId.ToString())
        .Replace("{OP1}", P.Op1.OperationId.ToString()).Replace("{OP2}", P.Op2.OperationId.ToString()).Replace("{OP3}", P.Op3.OperationId.ToString())
        .Replace("{OP4}", P.Op4.OperationId.ToString()).Replace("{OP5}", P.Op5.OperationId.ToString()).Replace("{OP9}", P.Op9.OperationId.ToString())
        .Replace("{OG1}", P.Og1.OperationGroupId.ToString()).Replace("{OG2}", P.Og2.OperationGroupId.ToString()).Replace("{OG9}", P.Og9.OperationGroupId.ToString())
        .Replace("{PD1}", P.PrD1.PieceRateId.ToString()).Replace("{PP1}", P.PrP1.PieceRateId.ToString()).Replace("{PA1}", P.PrA1.PieceRateId.ToString())
        .Replace("{PRR1}", P.PrR1.PieceRateId.ToString()).Replace("{PP9}", P.PrP9.PieceRateId.ToString());

    /// <summary>Gửi lần lượt các request bằng <paramref name="who"/>; mỗi request phải trả một trong <paramref name="codes"/>, và DB không đổi.</summary>
    protected async Task DenyAsync(string? who, int[] codes, params (HttpMethod M, string Url)[] reqs)
    {
        var before = await Snap();
        foreach (var (m, url) in reqs)
        {
            var (res, _) = await Send(who, m, R(url), m == HttpMethod.Get ? null : new { });
            using (res)
            {
                if (codes.Length == 1) await AssertErrorAsync(res, codes[0]);
                else
                {
                    ((int)res.StatusCode).Should().BeOneOf(codes, $"{m} {url}");
                    ((int)res.StatusCode).Should().BeLessThan(500);
                }
            }
        }

        await AssertSame(before);
    }

    // ------------------------------------------------------------------ DB

    protected async Task<Operation?> OpAsync(int id)
    {
        await using var db = NewDb();
        return await db.Operations.AsNoTracking().SingleOrDefaultAsync(o => o.OperationId == id);
    }

    protected async Task<Operation?> OpByCodeAsync(int productId, string code)
    {
        await using var db = NewDb();
        return await db.Operations.AsNoTracking().SingleOrDefaultAsync(o => o.ProductId == productId && o.OperationCode == code);
    }

    protected async Task<int> CountOpsAsync(int productId, string? code = null)
    {
        await using var db = NewDb();
        return await db.Operations.AsNoTracking().CountAsync(o => o.ProductId == productId && (code == null || o.OperationCode == code));
    }

    protected async Task<PieceRate> RateAsync(int id)
    {
        await using var db = NewDb();
        return await db.PieceRates.AsNoTracking().SingleAsync(r => r.PieceRateId == id);
    }

    protected async Task<List<PieceRate>> RatesOfAsync(int opId, DateOnly? date = null)
    {
        await using var db = NewDb();
        return await db.PieceRates.AsNoTracking().Where(r => r.OperationId == opId && (date == null || r.EffectiveDate == date)).ToListAsync();
    }

    protected async Task<OperationGroup?> GroupAsync(int id)
    {
        await using var db = NewDb();
        return await db.OperationGroups.AsNoTracking().SingleOrDefaultAsync(g => g.OperationGroupId == id);
    }

    protected async Task<List<OperationGroup>> GroupsOfAsync(int productId, string? name = null)
    {
        await using var db = NewDb();
        return await db.OperationGroups.AsNoTracking().Where(g => g.ProductId == productId && (name == null || g.GroupName == name)).ToListAsync();
    }

    protected async Task<List<int>> OpComponentsAsync(int opId)
    {
        await using var db = NewDb();
        return await db.OperationComponents.AsNoTracking().Where(c => c.OperationId == opId).Select(c => c.ProductComponentId).ToListAsync();
    }

    protected async Task<int> FinalActiveCountAsync(int productId)
    {
        await using var db = NewDb();
        return await db.Operations.AsNoTracking().CountAsync(o => o.ProductId == productId && o.IsFinalStep && o.Status == RecordStatus.Active);
    }

    protected async Task<List<ProductMarker>> MarkersAsync(int orderId, int productId)
    {
        await using var db = NewDb();
        return await db.ProductMarkers.AsNoTracking().Where(m => m.OrderId == orderId && m.ProductId == productId).OrderBy(m => m.MarkerNo).ToListAsync();
    }

    protected async Task<List<ProductMarkerRatio>> RatiosAsync(int markerId)
    {
        await using var db = NewDb();
        return await db.ProductMarkerRatios.AsNoTracking().Where(r => r.ProductMarkerId == markerId).ToListAsync();
    }

    protected async Task<ProductComponent> CompAsync(int id)
    {
        await using var db = NewDb();
        return await db.ProductComponents.AsNoTracking().SingleAsync(c => c.ProductComponentId == id);
    }

    protected async Task<List<ProductComponent>> CompsOfAsync(int productId)
    {
        await using var db = NewDb();
        return await db.ProductComponents.AsNoTracking().Where(c => c.ProductId == productId).ToListAsync();
    }

    protected async Task<List<ProductComponentMaterial>> NormsOfAsync(int productId)
    {
        await using var db = NewDb();
        return await db.ProductComponentMaterials.AsNoTracking().Where(n => n.ProductId == productId).ToListAsync();
    }

    protected async Task<int> NonExistingAsync() => await MissingIdAsync();

    /// <summary>Ảnh chụp toàn bộ dữ liệu B2 (Product / spec / bìa …) + công đoạn, nhóm, chi tiết công đoạn, đơn giá, tỉ lệ bìa.</summary>
    protected async Task<string> Snap()
    {
        var b2 = await SnapshotB2Async();
        await using var db = NewDb();
        var ops = await db.Operations.AsNoTracking().OrderBy(x => x.OperationId).Select(x => new
        {
            x.OperationId, x.ProductId, x.OperationGroupId, x.OperationCode, x.OperationName, x.StationSequence, x.SkillLevel,
            x.StandardTimeSec, x.TeamType, x.IsFinalStep, x.Status,
        }).ToListAsync();
        var groups = await db.OperationGroups.AsNoTracking().OrderBy(x => x.OperationGroupId).Select(x => new { x.OperationGroupId, x.ProductId, x.GroupName }).ToListAsync();
        var ocs = await db.OperationComponents.AsNoTracking().OrderBy(x => x.OperationId).ThenBy(x => x.ProductComponentId)
            .Select(x => new { x.OperationId, x.ProductComponentId, x.ProductId }).ToListAsync();
        var rates = await db.PieceRates.AsNoTracking().OrderBy(x => x.PieceRateId).Select(x => new
        {
            x.PieceRateId, x.OperationId, x.Rate, x.EffectiveDate, x.Status, x.SubmittedAt, x.ApprovedAt, x.DecisionNote,
        }).ToListAsync();
        var ratios = await db.ProductMarkerRatios.AsNoTracking().OrderBy(x => x.ProductMarkerId).ThenBy(x => x.SizeCode)
            .Select(x => new { x.ProductMarkerId, x.SizeCode, x.PiecesPerPly }).ToListAsync();
        return b2 + JsonSerializer.Serialize(new { ops, groups, ocs, rates, ratios });
    }

    protected async Task AssertSame(string before, string because = "") =>
        (await Snap()).Should().Be(before, $"[DB] không được thay đổi. {because}");

    // ------------------------------------------------------------------ body mẫu

    protected static object Comp(string name, string type, int pieces, int? id = null) =>
        new { ProductComponentId = id, ComponentName = name, ComponentType = type, PiecesPerGarment = pieces };

    protected static object Norm(string comp, string color, int materialId, string? note = null) =>
        new { ComponentName = comp, Color = color, MaterialId = materialId, SpecNote = note };

    protected static object MComp(int no, string comp) => new { MarkerNo = no, ComponentName = comp };
    protected static object MColor(int no, string color) => new { MarkerNo = no, Color = color };

    protected object[] BaseComps() =>
    [
        Comp("Thân trước", "PANEL", 1, B.Pr1Front.ProductComponentId),
        Comp("Túi", "PANEL", 2, B.Pr1Pocket.ProductComponentId),
        Comp("Nhãn", "TRIM", 1, B.Pr1Label.ProductComponentId),
    ];

    protected object[] BaseVariants() =>
    [
        new { ProductVariantId = (int?)B.Pr1VarM.ProductVariantId, Color = "Đen", SizeCode = "M" },
        new { ProductVariantId = (int?)B.Pr1VarL.ProductVariantId, Color = "Đen", SizeCode = "L" },
        new { ProductVariantId = (int?)P.Pr1WhiteM.ProductVariantId, Color = "Trắng", SizeCode = "M" },
    ];

    protected object SpecBody(object[]? comps = null, object[]? variants = null, object[]? norms = null, object[]? mcomps = null, object[]? mcolors = null) => new
    {
        Components = comps ?? BaseComps(),
        Variants = variants ?? BaseVariants(),
        Norms = norms ?? [Norm("Thân trước", "Đen", B.Ma1.MaterialId)],
        MarkerComponents = mcomps ?? [MComp(1, "Thân trước")],
        MarkerColors = mcolors ?? [MColor(1, "Đen")],
    };

    protected static object Ratio(string size, int ppp) => new { SizeCode = size, PiecesPerPly = ppp };

    protected static object OMarker(string code, int? no, decimal? len, decimal? wid, string status = "ACTIVE", object[]? ratios = null, int row = 1) => new
    {
        RowNumber = row, ProductCode = (string?)null, MarkerNo = no, MarkerCode = code, LengthM = len, WidthM = wid, Status = status,
        Ratios = ratios ?? [Ratio("M", 1), Ratio("L", 1)], ParseError = (string?)null,
    };

    protected static object Workbook(object[]? comps = null, object[]? norms = null, object[]? groups = null, object[]? markers = null) => new
    {
        HasComponents = comps is not null, Components = comps ?? [],
        HasNorms = norms is not null, Norms = norms ?? [],
        HasMarkerGroups = groups is not null, MarkerGroups = groups ?? [],
        HasOrderMarkers = markers is not null, OrderMarkers = markers ?? [],
        SizeColumns = new[] { "M", "L" },
    };

    protected static object XComp(int row, string name, string? type = "PANEL", int? pieces = 1, string? err = null) =>
        new { RowNumber = row, Name = name, ComponentType = type, PiecesPerGarment = pieces, ParseError = err };

    /// <summary>StationSequence mặc định suy từ mã công đoạn để mọi công đoạn tạo trong test có thứ tự khác nhau (hệ thống chặn trùng).</summary>
    protected static object SaveOp(int productId, string code, string name = "Công đoạn IT", int? seq = null, int? skill = null,
        decimal? time = null, string? team = "Sewing", bool final = false, string? group = null, int[]? comps = null) => new
    {
        ProductId = productId, OperationCode = code, OperationName = name, StationSequence = seq ?? 100 + code.Sum(c => c) % 30000, SkillLevel = skill, StandardTimeSec = time,
        TeamType = team, IsFinalStep = final, GroupName = group, ComponentIds = comps,
    };

    protected static object OpRow(int row, int stt, string? name, decimal? rate = null, string? group = null, string[]? comps = null,
        bool final = false, string? err = null, int? skill = null) => new
    {
        RowNumber = row, Stt = stt, Name = name, SkillLevel = skill, StandardTimeSec = (decimal?)null, Equipment = (string?)null, Rate = rate,
        ParseError = err, GroupName = group, ComponentNames = comps, IsFinal = final,
    };

    protected static object Sheet(params object[] rows) => new
    {
        SheetName = "May", ProductText = (string?)null, HasRateColumn = true, Rows = rows, HasGroupColumn = true, HasComponentColumn = true,
    };

    protected static string Dated(DateOnly d) => d.ToString("yyyy-MM-dd");

    /// <summary>Tìm đệ quy object đầu tiên có thuộc tính <paramref name="prop"/> = <paramref name="value"/>; bỏ qua nhánh <paramref name="skip"/>.</summary>
    protected static JsonElement? FindBy(JsonElement el, string prop, string value, JsonElement? skip = null)
    {
        if (skip is { } s && ReferenceEquals(s, el)) return null;
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (B1Json.Str(el, prop) == value) return el;
            foreach (var p in el.EnumerateObject())
            {
                if (skip is { } sk && p.Value.ValueKind == sk.ValueKind && p.Value.GetRawText() == sk.GetRawText()) continue;
                if (FindBy(p.Value, prop, value, skip) is { } hit) return hit;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
            foreach (var item in el.EnumerateArray())
                if (FindBy(item, prop, value, skip) is { } hit) return hit;
        return null;
    }
}
