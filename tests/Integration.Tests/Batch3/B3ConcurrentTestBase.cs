using FluentAssertions;
using Integration.Tests.Batch1;
using Integration.Tests.Batch2;
using Integration.Tests.Seed;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Infrastructure.Data;
using Xunit.Abstractions;

namespace Integration.Tests.Batch3;

/// <summary>Nền cho ca đồng thời / fault-injection IT_B3: dữ liệu S0 (Batch 1 + B2 + B3) được COMMIT và tự dọn sau test (PieceRate → Operation → OperationGroup được thêm vào danh sách bảng gốc).</summary>
public abstract class B3ConcurrentTestBase : B2ConcurrentTestBase
{
    protected B3Context P { get; private set; } = null!;

    protected B3ConcurrentTestBase(ITestOutputHelper output) : base(output) { }

    protected override string[] ExtraRootTables => ["PieceRate", "Operation", "OperationGroup"];

    protected override async Task OnSeededAsync(TestDataSeeder seeder, NxMtdDbContext seedDb) => P = await seeder.SeedBatch3BaseAsync(B);

    protected static readonly DateOnly D1201 = new(2026, 12, 1);

    /// <summary>trg_PieceRate_Approved chặn xóa đơn giá đã duyệt (kể cả dòng S0): tắt trong lúc dọn rồi bật lại.</summary>
    protected override async Task BeforeCleanupAsync(SqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DISABLE TRIGGER dbo.trg_PieceRate_Approved ON dbo.PieceRate";
        await cmd.ExecuteNonQueryAsync();
    }

    protected override async Task AfterCleanupAsync(SqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "ENABLE TRIGGER dbo.trg_PieceRate_Approved ON dbo.PieceRate";
        await cmd.ExecuteNonQueryAsync();
    }

    protected static int SeqOf(string code) => 100 + code.Sum(c => c) % 30000;

    protected Task<Api> DeleteAsync(string url, string? token) => SendAsync(HttpMethod.Delete, url, token, null);

    protected static object Comp(string name, string type, int pieces, int? id = null) =>
        new { ProductComponentId = id, ComponentName = name, ComponentType = type, PiecesPerGarment = pieces };

    protected object SpecBody(params string[] extra) => new
    {
        Components = (object[])[
            Comp("Thân trước", "PANEL", 1, B.Pr1Front.ProductComponentId), Comp("Túi", "PANEL", 2, B.Pr1Pocket.ProductComponentId),
            Comp("Nhãn", "TRIM", 1, B.Pr1Label.ProductComponentId), .. extra.Select(e => Comp(e, "PANEL", 1))],
        Variants = new[]
        {
            new { ProductVariantId = (int?)B.Pr1VarM.ProductVariantId, Color = "Đen", SizeCode = "M" },
            new { ProductVariantId = (int?)B.Pr1VarL.ProductVariantId, Color = "Đen", SizeCode = "L" },
            new { ProductVariantId = (int?)P.Pr1WhiteM.ProductVariantId, Color = "Trắng", SizeCode = "M" },
        },
        Norms = new[] { new { ComponentName = "Thân trước", Color = "Đen", MaterialId = B.Ma1.MaterialId, SpecNote = (string?)null } },
        MarkerComponents = new[] { new { MarkerNo = 1, ComponentName = "Thân trước" } },
        MarkerColors = new[] { new { MarkerNo = 1, Color = "Đen" } },
    };

    protected static object OpBody(int productId, string code, bool final = false) => new
    {
        ProductId = productId, OperationCode = code, OperationName = "Công đoạn IT", StationSequence = SeqOf(code), SkillLevel = (int?)null,
        StandardTimeSec = (decimal?)null, TeamType = "Sewing", IsFinalStep = final, GroupName = (string?)null, ComponentIds = (int[]?)null,
    };

    protected static object OMarkerWorkbook(string code, int no) => new
    {
        HasComponents = false, Components = Array.Empty<object>(), HasNorms = false, Norms = Array.Empty<object>(),
        HasMarkerGroups = false, MarkerGroups = Array.Empty<object>(), HasOrderMarkers = true,
        OrderMarkers = new[]
        {
            new
            {
                RowNumber = 1, ProductCode = (string?)null, MarkerNo = (int?)no, MarkerCode = code, LengthM = (decimal?)6.00m, WidthM = (decimal?)1.50m,
                Status = "ACTIVE", Ratios = new[] { new { SizeCode = "M", PiecesPerPly = 1 }, new { SizeCode = "L", PiecesPerPly = 1 } },
                ParseError = (string?)null,
            },
        },
        SizeColumns = new[] { "M", "L" },
    };

    /// <summary>Tạo trigger THROW test-only trên <paramref name="table"/>, chạy <paramref name="act"/>, luôn DROP trigger sau đó.</summary>
    protected async Task WithFaultTriggerAsync(string table, Func<Task> act)
    {
        var name = "trg_it_b3_fault_" + table;
        await Exec($"CREATE TRIGGER dbo.{name} ON dbo.{table} AFTER INSERT AS BEGIN THROW 50001, 'it-b3 fault injection', 1; END");
        try { await act(); }
        finally { await Exec($"IF OBJECT_ID('dbo.{name}', 'TR') IS NOT NULL DROP TRIGGER dbo.{name}"); }
    }

    private async Task Exec(string sql)
    {
        await using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    protected async Task<string> CountsAsync()
    {
        await using var db = NewDb();
        var c = await db.ProductComponents.AsNoTracking().CountAsync(x => x.ProductId == B.Pr1.ProductId);
        var n = await db.ProductComponentMaterials.AsNoTracking().CountAsync(x => x.ProductId == B.Pr1.ProductId);
        var f = await db.ProductMarkerFabrics.AsNoTracking().CountAsync(x => x.ProductId == B.Pr1.ProductId);
        var o = await db.Operations.AsNoTracking().CountAsync(x => x.ProductId == B.Pr1.ProductId);
        var g = await db.OperationGroups.AsNoTracking().CountAsync(x => x.ProductId == B.Pr1.ProductId);
        var oc = await db.OperationComponents.AsNoTracking().CountAsync(x => x.ProductId == B.Pr1.ProductId);
        var r = await db.PieceRates.AsNoTracking().CountAsync();
        return $"comp={c};norm={n};fabric={f};op={o};grp={g};oc={oc};rate={r}";
    }
}
