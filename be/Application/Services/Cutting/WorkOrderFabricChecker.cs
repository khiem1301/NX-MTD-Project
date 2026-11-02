using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Một dòng lệnh cắt: cả bộ (ProductComponentMaterialId = null) hoặc cắt lẻ thêm một chi tiết.</summary>
public sealed record CutLine(int ProductVariantId, int PlannedQty, string VariantLabel, int? ProductComponentMaterialId = null);

/// <summary>
/// Kiểm vải của lệnh cắt thường (<see cref="PieceFabricEstimator"/>), riêng từng NVL vải: có hình nào rộng hơn khổ vải không, và số mét
/// cần có đủ so với kho khả dụng + vải tổ cắt đang giữ không. Cả bộ tính theo bìa của mã hàng (DEV-ALL-42): mỗi sản phẩm một bìa, vải của
/// bìa tra theo màu áo, số sản phẩm cùng vải, bìa, size được gộp lại. Cắt lẻ tính theo kích thước chi tiết trong định mức, số miếng do
/// người lập nhập. Vải trong định mức của màu mà chưa nằm trên bìa nào thì báo chưa tính được. Chỉ để cảnh báo, không chặn lập lệnh.
/// </summary>
public sealed class WorkOrderFabricChecker
{
    private const int MaxNamesInMessage = 3;
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly FabricPlanner _planner;

    public WorkOrderFabricChecker(IUnitOfWork uow, IStockService stock, FabricPlanner planner)
    {
        _uow = uow; _stock = stock; _planner = planner;
    }

    private sealed record Fabric(int MaterialId, string Code, string Name, string Color, string Unit, decimal? WidthCm)
    {
        public string Label => $"{Code} {Name}{(Color == "" ? "" : $" ({Color})")}";
    }

    /// <summary>Một hình cần cắt trên một vải: bìa ở một size, hoặc chi tiết cắt lẻ. Thiếu kích thước thì chưa tính được.</summary>
    private sealed record Piece(Fabric Fabric, string Label, int Pieces, decimal? LengthCm, decimal? WidthCm);

    private sealed record Variant(int ProductVariantId, int ProductId, string Color, string SizeCode);

    private sealed record Norm(int ProductComponentMaterialId, int ProductVariantId, Fabric Fabric, string ComponentName,
        decimal? LengthCm, decimal? WidthCm);

    private sealed record MarkerSize(int ProductId, byte MarkerNo, string SizeCode, decimal LengthCm, decimal WidthCm);

    private sealed record MarkerFabric(int ProductId, byte MarkerNo, string Color, Fabric Fabric);

    public async Task<IReadOnlyList<WorkOrderFabricCheckDto>> CheckAsync(int orderId, int cuttingTeamId,
        IReadOnlyList<CutLine> lines, CancellationToken ct)
    {
        var variantIds = lines.Select(l => l.ProductVariantId).Distinct().ToArray();
        var variants = await _uow.Repository<ProductVariant>().Query().AsNoTracking()
            .Where(v => variantIds.Contains(v.ProductVariantId))
            .Select(v => new Variant(v.ProductVariantId, v.ProductId, v.Color, v.SizeCode))
            .ToDictionaryAsync(v => v.ProductVariantId, ct);
        var norms = await LoadNormsAsync(variantIds, ct);
        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToArray();
        var markers = await _uow.Repository<ProductMarker>().Query().AsNoTracking().Where(m => productIds.Contains(m.ProductId))
            .Select(m => new MarkerSize(m.ProductId, m.MarkerNo, m.SizeCode, m.LengthCm, m.WidthCm)).ToListAsync(ct);
        var fabrics = await _uow.Repository<ProductMarkerFabric>().Query().AsNoTracking().Where(f => productIds.Contains(f.ProductId))
            .Select(f => new MarkerFabric(f.ProductId, f.MarkerNo, f.Color, new Fabric(f.MaterialId, f.Material.MaterialCode, f.Material.MaterialName,
                f.Material.Color, f.Material.Unit, f.Material.WidthCm)))
            .ToListAsync(ct);

        var whole = lines.Where(l => l.ProductComponentMaterialId is null && l.PlannedQty > 0 && variants.ContainsKey(l.ProductVariantId)).ToList();
        var pieces = ExtraPieces(lines, norms).Concat(MarkerPieces(whole, variants, markers, fabrics))
            .Concat(UnplacedFabrics(whole, variants, norms, fabrics)).ToList();

        var result = new List<WorkOrderFabricCheckDto>();
        foreach (var fabric in pieces.GroupBy(p => p.Fabric.MaterialId).OrderBy(g => g.First().Fabric.Code))
            result.Add(await CheckFabricAsync(orderId, cuttingTeamId, fabric.ToList(), ct));

        var known = norms.Select(n => n.ProductVariantId).ToHashSet();
        var noFabric = lines.Where(l => !known.Contains(l.ProductVariantId) && !HasMarkerFabric(l, variants, fabrics))
            .Select(l => l.VariantLabel).Distinct().ToList();
        if (noFabric.Count > 0)
            result.Add(new WorkOrderFabricCheckDto(0, string.Empty, string.Empty, string.Empty, FabricCheckStatuses.Unknown, null, null,
                string.Format(WarehouseMessages.FabricCheckNoNorm, Names(noFabric))));
        return result;
    }

    /// <summary>Định mức chi tiết cắt còn dùng, NVL loại vải, của các biến thể trên lệnh.</summary>
    private async Task<List<Norm>> LoadNormsAsync(int[] variantIds, CancellationToken ct) =>
        await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => variantIds.Contains(p.ProductVariantId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                        && p.ProductComponent.Status == RecordStatus.Active && p.Material.MaterialType == MaterialTypes.Fabric)
            .Select(p => new Norm(p.ProductComponentMaterialId, p.ProductVariantId,
                new Fabric(p.MaterialId, p.Material.MaterialCode, p.Material.MaterialName, p.Material.Color, p.Material.Unit, p.Material.WidthCm),
                p.ProductComponent.ComponentName, p.LengthCm, p.WidthCm))
            .ToListAsync(ct);

    /// <summary>Cắt lẻ: số miếng do người lập nhập, kích thước của chi tiết trong định mức.</summary>
    private static IEnumerable<Piece> ExtraPieces(IReadOnlyList<CutLine> lines, List<Norm> norms) =>
        from line in lines
        from n in norms
        where line.ProductComponentMaterialId == n.ProductComponentMaterialId && line.PlannedQty > 0
        select new Piece(n.Fabric, $"{n.ComponentName} {line.VariantLabel}", line.PlannedQty, n.LengthCm, n.WidthCm);

    /// <summary>Cả bộ: mỗi sản phẩm một bìa; số sản phẩm cùng vải, bìa, size được gộp lại để tính đúng số hàng trên vải đó.</summary>
    private static IEnumerable<Piece> MarkerPieces(List<CutLine> whole, Dictionary<int, Variant> variants, List<MarkerSize> markers,
        List<MarkerFabric> fabrics)
    {
        var rows = from line in whole
                   let v = variants[line.ProductVariantId]
                   from f in fabrics
                   where f.ProductId == v.ProductId && SameColor(f.Color, v.Color)
                   select (f.Fabric, v.ProductId, f.MarkerNo, v.SizeCode, line.PlannedQty);
        foreach (var group in rows.GroupBy(r => (r.Fabric.MaterialId, r.ProductId, r.MarkerNo, r.SizeCode)))
        {
            var key = group.Key;
            var size = markers.FirstOrDefault(m => m.ProductId == key.ProductId && m.MarkerNo == key.MarkerNo
                                                   && string.Equals(m.SizeCode, key.SizeCode, StringComparison.OrdinalIgnoreCase));
            yield return new Piece(group.First().Fabric, string.Format(WarehouseMessages.FabricCheckMarkerLabel, key.MarkerNo, key.SizeCode),
                group.Sum(r => r.PlannedQty), size?.LengthCm, size?.WidthCm);
        }
    }

    /// <summary>Vải có trong định mức của màu mà chưa nằm trên bìa nào: phải báo, nếu không tổng mét sẽ thiếu mà vẫn báo đủ.</summary>
    private static IEnumerable<Piece> UnplacedFabrics(List<CutLine> whole, Dictionary<int, Variant> variants, List<Norm> norms,
        List<MarkerFabric> fabrics) =>
        from line in whole
        let v = variants[line.ProductVariantId]
        from n in norms
        where n.ProductVariantId == v.ProductVariantId
              && !fabrics.Any(f => f.ProductId == v.ProductId && SameColor(f.Color, v.Color) && f.Fabric.MaterialId == n.Fabric.MaterialId)
        select new Piece(n.Fabric, string.Format(WarehouseMessages.FabricCheckNoMarkerLabel, v.Color), line.PlannedQty, null, null);

    private static bool HasMarkerFabric(CutLine line, Dictionary<int, Variant> variants, List<MarkerFabric> fabrics) =>
        variants.TryGetValue(line.ProductVariantId, out var v)
        && fabrics.Any(f => f.ProductId == v.ProductId && SameColor(f.Color, v.Color));

    private static bool SameColor(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private async Task<WorkOrderFabricCheckDto> CheckFabricAsync(int orderId, int cuttingTeamId, IReadOnlyList<Piece> pieces, CancellationToken ct)
    {
        var f = pieces[0].Fabric;
        WorkOrderFabricCheckDto Row(string status, string? message, decimal? need = null, decimal? available = null) =>
            new(f.MaterialId, f.Code, f.Name, f.Color, status, need, available, message);

        if (f.Unit != MaterialTypes.FabricUnit) return Row(FabricCheckStatuses.Unknown, string.Format(WarehouseMessages.FabricCheckNotMeters, f.Label));
        if (f.WidthCm is null) return Row(FabricCheckStatuses.Unknown, string.Format(WarehouseMessages.FabricCheckNoWidth, f.Label));

        var tooWide = pieces.Where(p => p.WidthCm > f.WidthCm).ToList();
        if (tooWide.Count > 0)
            return Row(FabricCheckStatuses.TooWide, string.Format(WarehouseMessages.FabricCheckTooWide, f.Label, Format(f.WidthCm.Value / MaterialTypes.CmPerMeter),
                Names(tooWide.Select(p => $"{p.Label} ({Format(p.WidthCm!.Value)} cm)").ToList())));

        var noSize = pieces.Where(p => p.LengthCm is not > 0 || p.WidthCm is not > 0).Select(p => p.Label).Distinct().ToList();
        if (noSize.Count > 0)
            return Row(FabricCheckStatuses.Unknown, string.Format(WarehouseMessages.FabricCheckNoSize, f.Label, Names(noSize)));

        var need = pieces.Sum(p => PieceFabricEstimator.NeedMeters(p.Pieces, p.LengthCm, p.WidthCm, f.WidthCm)!.Value);
        var warehouse = await _stock.GetAvailableAsync(StockKey.Material(f.MaterialId), ct);
        var holding = Math.Max(0, await _planner.GetTeamHoldingAsync(cuttingTeamId, orderId, f.MaterialId, ct));
        var available = warehouse + holding;
        if (available >= need) return Row(FabricCheckStatuses.Enough, null, need, available);
        return Row(FabricCheckStatuses.Short, string.Format(WarehouseMessages.FabricCheckShort, f.Label, Format(need - available), Format(need),
            Format(available), Format(warehouse), Format(holding)), need, available);
    }

    /// <summary>Tối đa 3 tên để cảnh báo không quá dài; còn lại ghi số lượng.</summary>
    private static string Names(IReadOnlyList<string> names) =>
        names.Count <= MaxNamesInMessage
            ? string.Join(", ", names)
            : $"{string.Join(", ", names.Take(MaxNamesInMessage))} và {names.Count - MaxNamesInMessage} mục khác";

    private static string Format(decimal value) => value.ToString("#,0.###", Vietnamese);
}
