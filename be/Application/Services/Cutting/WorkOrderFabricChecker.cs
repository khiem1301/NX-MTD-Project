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
/// Kiểm vải của lệnh cắt theo bìa của đơn (DEV-ALL-85), riêng từng NVL vải: số mét cần (<see cref="MarkerFabricEstimator"/>) có đủ so với
/// kho khả dụng + vải tổ cắt đang giữ không. Cả bộ: nhu cầu gom theo (nhóm bìa, vải) — vải tra theo nhóm bìa × màu áo ở mã hàng — rồi
/// ghép các bìa của đơn cho ít mét nhất. Cắt lẻ: chi tiết tìm nhóm bìa chứa nó và trải trên đúng vải. Thiếu bìa / nhóm bìa thì báo chưa
/// tính được. Luôn trả số mét cần và bìa nên dùng (hệ thống ước tính, có thể chênh lệch với thực tế). Chỉ để cảnh báo, không lưu, không chặn.
/// </summary>
public sealed class WorkOrderFabricChecker
{
    private const int MaxNamesInMessage = 3;
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly FabricPlanner _planner;
    private readonly IMarkerPlanSolver _solver;

    public WorkOrderFabricChecker(IUnitOfWork uow, IStockService stock, FabricPlanner planner, IMarkerPlanSolver solver)
    {
        _uow = uow; _stock = stock; _planner = planner; _solver = solver;
    }

    private sealed record Fabric(int MaterialId, string Code, string Name, string Color, string Unit)
    {
        public string Label => $"{Code} {Name}{(Color == "" ? "" : $" ({Color})")}";
    }

    private sealed record Variant(int ProductVariantId, int ProductId, int ProductColorId, string Color, string SizeCode);

    /// <summary>Dòng định mức vải của chi tiết cắt, theo màu áo (DEV-ALL-87): size lấy từ dòng lệnh.</summary>
    private sealed record Norm(int ProductComponentMaterialId, int ProductColorId, int ProductComponentId, string ComponentName,
        short PiecesPerGarment, Fabric Fabric);

    private sealed record GroupFabric(int ProductId, byte MarkerNo, string Color, Fabric Fabric);

    private sealed record GroupComponent(int ProductId, byte MarkerNo, int ProductComponentId);

    private sealed record OrderMarker(int ProductId, byte MarkerNo, MarkerOption Option);

    private sealed record Data(Dictionary<int, Variant> Variants, List<Norm> Norms, List<GroupFabric> Fabrics,
        List<GroupComponent> Components, List<OrderMarker> Markers);

    /// <summary>Số mét ước tính của một phần việc trên một vải; Problem = chưa tính được, Plan = bìa nên dùng.</summary>
    private sealed record Estimate(Fabric Fabric, decimal Meters, string? Problem = null, FabricMarkerPlanDto? Plan = null);

    public async Task<IReadOnlyList<WorkOrderFabricCheckDto>> CheckAsync(int orderId, int cuttingTeamId,
        IReadOnlyList<CutLine> lines, CancellationToken ct)
    {
        var data = await LoadAsync(orderId, lines, ct);
        var whole = lines.Where(l => l.ProductComponentMaterialId is null && l.PlannedQty > 0 && data.Variants.ContainsKey(l.ProductVariantId)).ToList();
        var estimates = WholeEstimates(whole, data).Concat(ExtraEstimates(lines, data))
            .Concat(UnplacedFabrics(whole, data)).ToList();

        var result = new List<WorkOrderFabricCheckDto>();
        foreach (var fabric in estimates.GroupBy(e => e.Fabric.MaterialId).OrderBy(g => g.First().Fabric.Code))
            result.Add(await CheckFabricAsync(orderId, cuttingTeamId, fabric.ToList(), ct));

        var known = data.Norms.Select(n => n.ProductColorId).ToHashSet();
        var noFabric = lines.Where(l => !(data.Variants.TryGetValue(l.ProductVariantId, out var v) && known.Contains(v.ProductColorId)) && !HasGroupFabric(l, data))
            .Select(l => l.VariantLabel).Distinct().ToList();
        if (noFabric.Count > 0)
            result.Add(new WorkOrderFabricCheckDto(0, string.Empty, string.Empty, string.Empty, FabricCheckStatuses.Unknown, null, null,
                string.Format(WarehouseMessages.FabricCheckNoNorm, Names(noFabric))));
        return result;
    }

    /// <summary>Đọc biến thể, định mức vải, nhóm bìa của mã hàng và bìa đang dùng của đơn cho các dòng lệnh.</summary>
    private async Task<Data> LoadAsync(int orderId, IReadOnlyList<CutLine> lines, CancellationToken ct)
    {
        var variantIds = lines.Select(l => l.ProductVariantId).Distinct().ToArray();
        var variants = await _uow.Repository<ProductVariant>().Query().AsNoTracking().Where(v => variantIds.Contains(v.ProductVariantId))
            .Select(v => new Variant(v.ProductVariantId, v.ProductId, v.ProductColorId, v.ProductColor.Color, v.SizeCode))
            .ToDictionaryAsync(v => v.ProductVariantId, ct);
        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToArray();
        var colorIds = variants.Values.Select(v => v.ProductColorId).Distinct().ToArray();
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => colorIds.Contains(p.ProductColorId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                        && p.ProductComponent.Status == RecordStatus.Active && p.Material.MaterialType == MaterialTypes.Fabric)
            .Select(p => new Norm(p.ProductComponentMaterialId, p.ProductColorId, p.ProductComponentId, p.ProductComponent.ComponentName,
                p.ProductComponent.PiecesPerGarment,
                new Fabric(p.MaterialId, p.Material.MaterialCode, p.Material.MaterialName, p.Material.Color, p.Material.Unit)))
            .ToListAsync(ct);
        var fabrics = await _uow.Repository<ProductMarkerFabric>().Query().AsNoTracking().Where(f => productIds.Contains(f.ProductId))
            .Select(f => new GroupFabric(f.ProductId, f.MarkerNo, f.ProductColor.Color,
                new Fabric(f.MaterialId, f.Material.MaterialCode, f.Material.MaterialName, f.Material.Color, f.Material.Unit)))
            .ToListAsync(ct);
        var components = await _uow.Repository<ProductMarkerComponent>().Query().AsNoTracking().Where(c => productIds.Contains(c.ProductId))
            .Select(c => new GroupComponent(c.ProductId, c.MarkerNo, c.ProductComponentId)).ToListAsync(ct);
        return new Data(variants, norms, fabrics, components, await LoadMarkersAsync(orderId, productIds, ct));
    }

    private async Task<List<OrderMarker>> LoadMarkersAsync(int orderId, int[] productIds, CancellationToken ct)
    {
        var rows = await _uow.Repository<ProductMarker>().Query().AsNoTracking()
            .Where(m => m.OrderId == orderId && productIds.Contains(m.ProductId) && m.Status == RecordStatus.Active)
            .Select(m => new
            {
                m.ProductId,
                m.MarkerNo,
                m.MarkerCode,
                m.LengthM,
                Ratios = m.ProductMarkerRatios.Select(r => new { r.SizeCode, r.PiecesPerPly }).ToList(),
            })
            .ToListAsync(ct);
        return rows.Select(m => new OrderMarker(m.ProductId, m.MarkerNo, new MarkerOption(m.MarkerCode, m.LengthM,
            m.Ratios.ToDictionary(r => r.SizeCode, r => r.PiecesPerPly, StringComparer.OrdinalIgnoreCase)))).ToList();
    }

    /// <summary>Cả bộ: nhu cầu mỗi size gom theo (mã hàng, nhóm bìa, vải), ghép các bìa của đơn trong nhóm cho ít mét nhất.</summary>
    private IEnumerable<Estimate> WholeEstimates(List<CutLine> whole, Data data)
    {
        var rows = from line in whole
                   let v = data.Variants[line.ProductVariantId]
                   from f in data.Fabrics
                   where f.ProductId == v.ProductId && SameText(f.Color, v.Color)
                   select (Key: (v.ProductId, f.MarkerNo, f.Fabric.MaterialId), f.Fabric, v.ProductColorId, v.Color, v.SizeCode, line.PlannedQty);
        foreach (var group in rows.GroupBy(r => r.Key))
        {
            var need = group.GroupBy(r => r.SizeCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.PlannedQty), StringComparer.OrdinalIgnoreCase);
            var (productId, markerNo, _) = group.Key;
            var choice = MarkerFabricEstimator.ChooseWhole(need, MarkersOf(data, productId, markerNo), _solver);
            var fabric = group.First().Fabric;
            if (choice is null)
            {
                yield return new Estimate(fabric, 0, string.Format(WarehouseMessages.FabricCheckNoOrderMarkerLabel, markerNo,
                    Names(group.Select(r => r.Color).Distinct().ToList()), string.Join(", ", need.Keys)));
                continue;
            }
            yield return new Estimate(fabric, choice.Meters, Plan: PlanDto(markerNo, isExtra: false, choice));
        }
    }

    /// <summary>Cắt lẻ / cắt bù: chi tiết tìm nhóm bìa chứa nó và trải trên đúng vải của dòng định mức, ghép bìa cho ít mét nhất.</summary>
    private IEnumerable<Estimate> ExtraEstimates(IReadOnlyList<CutLine> lines, Data data)
    {
        var rows = from line in lines
                   where line.ProductComponentMaterialId is not null && line.PlannedQty > 0
                   from n in data.Norms
                   where n.ProductComponentMaterialId == line.ProductComponentMaterialId && data.Variants.ContainsKey(line.ProductVariantId)
                   let v = data.Variants[line.ProductVariantId]
                   select (Line: line, Norm: n, Variant: v, Group: GroupOf(data, v, n));
        foreach (var missing in rows.Where(r => r.Group is null))
            yield return new Estimate(missing.Norm.Fabric, 0,
                string.Format(WarehouseMessages.FabricCheckNoMarkerGroupLabel, $"{missing.Norm.ComponentName} {missing.Line.VariantLabel}"));
        foreach (var group in rows.Where(r => r.Group is not null).GroupBy(r => (r.Variant.ProductId, r.Group!.Value, r.Norm.Fabric.MaterialId)))
        {
            var need = group.Select(r => new PieceNeed(r.Variant.SizeCode, r.Line.PlannedQty, r.Norm.PiecesPerGarment)).ToList();
            var choice = MarkerFabricEstimator.ChooseExtra(need, MarkersOf(data, group.Key.ProductId, group.Key.Value), _solver);
            var fabric = group.First().Norm.Fabric;
            yield return choice is null
                ? new Estimate(fabric, 0, string.Format(WarehouseMessages.FabricCheckNoOrderMarkerLabel, group.Key.Value,
                    Names(group.Select(r => r.Variant.Color).Distinct().ToList()),
                    string.Join(", ", need.Select(p => p.SizeCode).Distinct(StringComparer.OrdinalIgnoreCase))))
                : new Estimate(fabric, choice.Meters, Plan: PlanDto(group.Key.Value, isExtra: true, choice));
        }
    }

    /// <summary>Nhóm bìa (số nhỏ nhất) chứa chi tiết của dòng định mức và trải trên đúng vải của dòng đó với màu áo của biến thể.</summary>
    private static byte? GroupOf(Data data, Variant v, Norm n) =>
        data.Components.Where(c => c.ProductId == v.ProductId && c.ProductComponentId == n.ProductComponentId)
            .Select(c => c.MarkerNo)
            .Where(g => data.Fabrics.Any(f => f.ProductId == v.ProductId && f.MarkerNo == g && SameText(f.Color, v.Color)
                                              && f.Fabric.MaterialId == n.Fabric.MaterialId))
            .Order().Cast<byte?>().FirstOrDefault();

    /// <summary>Vải có trong định mức của màu mà chưa nằm trên nhóm bìa nào: phải báo, nếu không tổng mét sẽ thiếu mà vẫn báo đủ.</summary>
    private static IEnumerable<Estimate> UnplacedFabrics(List<CutLine> whole, Data data) =>
        from line in whole
        let v = data.Variants[line.ProductVariantId]
        from n in data.Norms
        where n.ProductColorId == v.ProductColorId
              && !data.Fabrics.Any(f => f.ProductId == v.ProductId && SameText(f.Color, v.Color) && f.Fabric.MaterialId == n.Fabric.MaterialId)
        select new Estimate(n.Fabric, 0, string.Format(WarehouseMessages.FabricCheckNoMarkerLabel, v.Color));

    /// <summary>Phương án của một nhóm bìa để hiển thị: từng bìa với số lớp, số mét và tỉ lệ size (theo thứ tự size).</summary>
    private static FabricMarkerPlanDto PlanDto(byte markerNo, bool isExtra, MarkerPlan plan) =>
        new(markerNo, isExtra, plan.Meters, plan.Surplus, plan.IsProvenOptimal, plan.Uses.Select(u => new FabricMarkerUseDto(
                u.Code, u.LengthM, u.Plies, u.Meters,
                (u.Ratio ?? new Dictionary<string, int>()).OrderBy(r => SizeOrder.Rank(r.Key)).ThenBy(r => r.Key, StringComparer.Ordinal)
                    .Select(r => new MarkerRatioDto(r.Key, (short)r.Value)).ToList()))
            .ToList());

    /// <summary>Bìa của đơn trong nhóm. Nhóm thuộc một dạng bìa (DEV-ALL-89) nên chỉ các màu của dạng đó đi vào nhóm và dùng các bìa này.</summary>
    private static IEnumerable<MarkerOption> MarkersOf(Data data, int productId, byte markerNo) =>
        data.Markers.Where(m => m.ProductId == productId && m.MarkerNo == markerNo).Select(m => m.Option);

    private static bool HasGroupFabric(CutLine line, Data data) =>
        data.Variants.TryGetValue(line.ProductVariantId, out var v)
        && data.Fabrics.Any(f => f.ProductId == v.ProductId && SameText(f.Color, v.Color));

    private static bool SameText(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private async Task<WorkOrderFabricCheckDto> CheckFabricAsync(int orderId, int cuttingTeamId, IReadOnlyList<Estimate> estimates, CancellationToken ct)
    {
        var f = estimates[0].Fabric;
        WorkOrderFabricCheckDto Row(string status, string? message) => new(f.MaterialId, f.Code, f.Name, f.Color, status, null, null, message);

        if (f.Unit != MaterialTypes.FabricUnit) return Row(FabricCheckStatuses.Unknown, string.Format(WarehouseMessages.FabricCheckNotMeters, f.Label));
        var problems = estimates.Where(e => e.Problem is not null).Select(e => e.Problem!).Distinct().ToList();
        if (problems.Count > 0)
            return Row(FabricCheckStatuses.Unknown, string.Format(WarehouseMessages.FabricCheckNoSize, f.Label, Names(problems)));

        var need = estimates.Sum(e => e.Meters);
        var warehouse = await _stock.GetAvailableAsync(StockKey.Material(f.MaterialId), ct);
        var holding = Math.Max(0, await _planner.GetTeamHoldingAsync(cuttingTeamId, orderId, f.MaterialId, ct));
        var available = warehouse + holding;
        var plans = estimates.Where(e => e.Plan is not null).Select(e => e.Plan!).OrderBy(p => p.IsExtra).ThenBy(p => p.MarkerNo).ToList();
        var message = available >= need ? null : string.Format(WarehouseMessages.FabricCheckShort, f.Label, Format(need - available),
            Format(need), Format(available), Format(warehouse), Format(holding));
        return new WorkOrderFabricCheckDto(f.MaterialId, f.Code, f.Name, f.Color,
            message is null ? FabricCheckStatuses.Enough : FabricCheckStatuses.Short, need, available, message, plans, warehouse, holding);
    }

    /// <summary>Tối đa 3 tên để cảnh báo không quá dài; còn lại ghi số lượng.</summary>
    private static string Names(IReadOnlyList<string> names) =>
        names.Count <= MaxNamesInMessage
            ? string.Join(", ", names)
            : $"{string.Join(", ", names.Take(MaxNamesInMessage))} và {names.Count - MaxNamesInMessage} mục khác";

    private static string Format(decimal value) => value.ToString("#,0.###", Vietnamese);
}
