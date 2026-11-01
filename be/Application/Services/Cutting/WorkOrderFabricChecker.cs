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
/// Kiểm vải của lệnh cắt thường theo kích thước từng chi tiết trong định mức (<see cref="PieceFabricEstimator"/>), riêng từng NVL vải:
/// chi tiết có rộng hơn khổ vải không, và số mét cần có đủ so với kho khả dụng + vải tổ cắt đang giữ không.
/// Cả bộ: số miếng = số bộ × số chi tiết trên một sản phẩm; cắt lẻ: số miếng do người lập nhập. Chỉ để cảnh báo, không chặn lập lệnh.
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

    private sealed record Piece(int MaterialId, string Code, string Name, string Color, string Unit, decimal? FabricWidthCm,
        string PieceLabel, int Pieces, decimal? LengthCm, decimal? WidthCm)
    {
        public string FabricLabel => $"{Code} {Name}{(Color == "" ? "" : $" ({Color})")}";
    }

    public async Task<IReadOnlyList<WorkOrderFabricCheckDto>> CheckAsync(int orderId, int cuttingTeamId,
        IReadOnlyList<CutLine> lines, CancellationToken ct)
    {
        var variantIds = lines.Select(l => l.ProductVariantId).Distinct().ToArray();
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => variantIds.Contains(p.ProductVariantId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                        && p.ProductComponent.Status == RecordStatus.Active && p.Material.MaterialType == MaterialTypes.Fabric)
            .Select(p => new
            {
                p.ProductComponentMaterialId, p.ProductVariantId, p.MaterialId, p.Material.MaterialCode, p.Material.MaterialName, p.Material.Color,
                p.Material.Unit, FabricWidthCm = p.Material.WidthCm, p.ProductComponent.ComponentName, p.ProductComponent.PiecesPerGarment,
                p.LengthCm, p.WidthCm,
            })
            .ToListAsync(ct);

        var pieces = new List<Piece>();
        foreach (var n in norms)
        {
            var ofVariant = lines.Where(l => l.ProductVariantId == n.ProductVariantId).ToList();
            var count = ofVariant.Where(l => l.ProductComponentMaterialId is null).Sum(l => l.PlannedQty * n.PiecesPerGarment)
                        + ofVariant.Where(l => l.ProductComponentMaterialId == n.ProductComponentMaterialId).Sum(l => l.PlannedQty);
            if (count > 0)
                pieces.Add(new Piece(n.MaterialId, n.MaterialCode, n.MaterialName, n.Color, n.Unit, n.FabricWidthCm,
                    $"{n.ComponentName} {ofVariant[0].VariantLabel}", count, n.LengthCm, n.WidthCm));
        }

        var result = new List<WorkOrderFabricCheckDto>();
        foreach (var fabric in pieces.GroupBy(p => p.MaterialId).OrderBy(g => g.First().Code))
            result.Add(await CheckFabricAsync(orderId, cuttingTeamId, fabric.ToList(), ct));

        var withNorm = norms.Select(n => n.ProductVariantId).ToHashSet();
        var noNorm = lines.Where(l => !withNorm.Contains(l.ProductVariantId)).Select(l => l.VariantLabel).Distinct().ToList();
        if (noNorm.Count > 0)
            result.Add(new WorkOrderFabricCheckDto(0, string.Empty, string.Empty, string.Empty, FabricCheckStatuses.Unknown, null, null,
                string.Format(WarehouseMessages.FabricCheckNoNorm, Names(noNorm))));
        return result;
    }

    private async Task<WorkOrderFabricCheckDto> CheckFabricAsync(int orderId, int cuttingTeamId, IReadOnlyList<Piece> pieces, CancellationToken ct)
    {
        var f = pieces[0];
        WorkOrderFabricCheckDto Row(string status, string? message, decimal? need = null, decimal? available = null) =>
            new(f.MaterialId, f.Code, f.Name, f.Color, status, need, available, message);

        if (f.Unit != "m") return Row(FabricCheckStatuses.Unknown, string.Format(WarehouseMessages.FabricCheckNotMeters, f.FabricLabel));
        if (f.FabricWidthCm is null) return Row(FabricCheckStatuses.Unknown, string.Format(WarehouseMessages.FabricCheckNoWidth, f.FabricLabel));

        var tooWide = pieces.Where(p => p.WidthCm > f.FabricWidthCm).ToList();
        if (tooWide.Count > 0)
            return Row(FabricCheckStatuses.TooWide, string.Format(WarehouseMessages.FabricCheckTooWide, f.FabricLabel, Format(f.FabricWidthCm.Value),
                Names(tooWide.Select(p => $"{p.PieceLabel} ({Format(p.WidthCm!.Value)} cm)").ToList())));

        var noSize = pieces.Where(p => p.LengthCm is not > 0 || p.WidthCm is not > 0).Select(p => p.PieceLabel).ToList();
        if (noSize.Count > 0)
            return Row(FabricCheckStatuses.Unknown, string.Format(WarehouseMessages.FabricCheckNoSize, f.FabricLabel, Names(noSize)));

        var need = pieces.Sum(p => PieceFabricEstimator.NeedMeters(p.Pieces, p.LengthCm, p.WidthCm, f.FabricWidthCm)!.Value);
        var warehouse = await _stock.GetAvailableAsync(StockKey.Material(f.MaterialId), ct);
        var holding = Math.Max(0, await _planner.GetTeamHoldingAsync(cuttingTeamId, orderId, f.MaterialId, ct));
        var available = warehouse + holding;
        if (available >= need) return Row(FabricCheckStatuses.Enough, null, need, available);
        return Row(FabricCheckStatuses.Short, string.Format(WarehouseMessages.FabricCheckShort, f.FabricLabel, Format(need - available), Format(need),
            Format(available), Format(warehouse), Format(holding)), need, available);
    }

    /// <summary>Tối đa 3 tên để cảnh báo không quá dài; còn lại ghi số lượng.</summary>
    private static string Names(IReadOnlyList<string> names) =>
        names.Count <= MaxNamesInMessage
            ? string.Join(", ", names)
            : $"{string.Join(", ", names.Take(MaxNamesInMessage))} và {names.Count - MaxNamesInMessage} mục khác";

    private static string Format(decimal value) => value.ToString("#,0.###", Vietnamese);
}
