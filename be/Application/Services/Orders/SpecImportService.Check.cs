using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-122/123: kiểm bảng định mức cho popup kiểm tra Excel, kể cả khi mã hàng chưa tạo (productId null), chưa có màu, chưa
/// chọn đơn hoặc dùng NVL chưa có trong danh mục. Màu / NVL chưa có được coi tạm là có để chạy đủ kiểm tra khác (chi tiết, trùng
/// dòng, PANEL); dòng dính màu đó trả Pending ("kiểm khi Lưu đơn"), dòng dùng NVL chưa có trả NewMaterial (cảnh báo). Không có đơn
/// thì dòng bìa của đơn là Pending (lỗi định dạng vẫn là Error). Không ghi gì.
/// </summary>
public sealed partial class SpecImportService
{
    private const string AssumedSize = "?";

    /// <summary>Màu / NVL coi tạm là có (khóa chữ hoa) để đánh dấu lại các dòng dính chúng.</summary>
    private sealed record Assumed(HashSet<string> Colors, HashSet<string> Materials);

    public async Task<Result<SpecImportPreviewDto>> CheckAsync(int? productId, int? orderId, SpecWorkbookDto workbook,
        CancellationToken ct, bool replace = false)
    {
        var start = await CheckStartAsync(productId, orderId, ct);
        if (!start.IsSuccess) return Fail.As<SpecImportPreviewDto>(start);
        var (spec, target) = start.Data!;

        var (assumedSpec, colors) = AssumeColors(spec, workbook);
        var (materials, missingMaterials) = await AssumeMaterialsAsync(workbook, ct);
        var assumed = new Assumed(colors, missingMaterials);

        // DEV-ALL-155: form sửa mã hàng — chế độ thay thế cần biết dòng đã dùng để liệt kê sẽ xóa / giữ.
        var usage = replace && productId is { } id ? await UsageAsync(id, spec, ct) : SpecImportUsage.None;
        var builder = new SpecImportBuilder(assumedSpec, materials, target.OrderProduct, target.ProductCode, workbook, replace, usage);
        // Kiểm của nút Lưu cần mã hàng thật, đủ màu và NVL có thật; thiếu thì để lúc nhập thật (sau Lưu đơn / sau khi thêm NVL).
        var crossErrors = productId is { } pid && colors.Count == 0 && missingMaterials.Count == 0
            ? await CrossErrorsAsync(pid, orderId, builder, ct) : [];
        var preview = ToPreview(productId ?? 0, orderId, target, workbook, builder, crossErrors);
        return Result<SpecImportPreviewDto>.Success(MarkAssumed(preview, workbook, target, assumed, builder.DuplicateCount));
    }

    /// <summary>Mã hàng có thì lấy định mức và đơn như xem trước thường; chưa tạo thì định mức rỗng, không có đơn.</summary>
    private async Task<Result<(ProductSpecDto Spec, Target Target)>> CheckStartAsync(int? productId, int? orderId, CancellationToken ct)
    {
        if (productId is not { } id)
            return Result<(ProductSpecDto, Target)>.Success((new ProductSpecDto(0, [], [], [], [], []), new Target(string.Empty, null, null)));
        var target = await TargetAsync(id, orderId, ct);
        if (!target.IsSuccess) return Fail.As<(ProductSpecDto, Target)>(target);
        var spec = await _specs.GetAsync(id, ct);
        return spec.IsSuccess
            ? Result<(ProductSpecDto, Target)>.Success((spec.Data!, target.Data!))
            : Fail.As<(ProductSpecDto, Target)>(spec);
    }

    private static string Up(string s) => s.Trim().ToUpperInvariant();

    /// <summary>Thêm biến thể tạm cho màu trong file mà mã hàng chưa có (màu tạo khi Lưu đơn).</summary>
    private static (ProductSpecDto Spec, HashSet<string> Missing) AssumeColors(ProductSpecDto spec, SpecWorkbookDto workbook)
    {
        var known = spec.Variants.Where(v => v.Status == "Active").Select(v => Up(v.Color)).ToHashSet();
        var missing = workbook.Norms.Select(n => n.Color).Concat(workbook.MarkerGroups.SelectMany(g => g.Colors))
            .Where(c => c.Trim().Length > 0 && !known.Contains(Up(c))).DistinctBy(Up).ToList();
        if (missing.Count == 0) return (spec, []);
        var variants = missing.Select(c => new ProductVariantDto(0, c.Trim(), AssumedSize, "Active"));
        return (spec with { Variants = [.. spec.Variants, .. variants] }, missing.Select(Up).ToHashSet());
    }

    /// <summary>NVL chưa có trong danh mục (hoặc ngừng dùng) coi tạm là có, Id âm để không trùng NVL thật.</summary>
    private async Task<(IReadOnlyDictionary<string, SpecImportMaterial> Materials, HashSet<string> Missing)> AssumeMaterialsAsync(
        SpecWorkbookDto workbook, CancellationToken ct)
    {
        var found = await MaterialsAsync(workbook.Norms.Select(n => n.MaterialCode), ct);
        var missing = workbook.Norms.Select(n => Up(n.MaterialCode))
            .Where(c => c.Length > 0 && !found.ContainsKey(c)).Distinct().ToList();
        if (missing.Count == 0) return (found, []);
        var temp = missing.Select((c, i) => KeyValuePair.Create(c, new SpecImportMaterial(-(i + 1), c, c, string.Empty, string.Empty)));
        return (new Dictionary<string, SpecImportMaterial>(found.Concat(temp), StringComparer.OrdinalIgnoreCase),
            missing.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>Đánh dấu lại dòng dính màu / NVL coi tạm (Pending / NewMaterial), bìa khi chưa có đơn, rồi đếm lại.</summary>
    private static SpecImportPreviewDto MarkAssumed(SpecImportPreviewDto preview, SpecWorkbookDto workbook, Target target,
        Assumed assumed, int duplicates)
    {
        var norms = preview.Norms.Select(n => MarkNorm(n, assumed)).ToList();
        var groups = preview.MarkerGroups.Select(g =>
            g.Action != SpecImportBuilder.ActionError && g.Colors.Any(c => assumed.Colors.Contains(Up(c)))
                ? g with { Action = SpecImportBuilder.ActionPending, Error = PendingColor(g.Colors, assumed) } : g).ToList();
        var markers = target.OrderProduct is null
            ? workbook.OrderMarkers.Select(PendingMarker).ToList()
            : preview.OrderMarkers.ToList();

        var actions = preview.Components.Select(c => c.Action).Concat(norms.Select(n => n.Action))
            .Concat(groups.Select(g => g.Action)).Concat(markers.Select(m => m.Action)).ToList();
        var errors = actions.Count(a => a == SpecImportBuilder.ActionError);
        return preview with
        {
            Norms = norms,
            MarkerGroups = groups,
            OrderMarkers = markers,
            HasOrderMarkers = workbook.HasOrderMarkers,
            ValidCount = actions.Count - errors,
            ErrorCount = errors - duplicates,
            Notes = preview.Notes.Where(n => n != Messages.SpecImportMarkersSkipped && n != Messages.SpecImportNoChange).ToList(),
        };
    }

    private static SpecImportNormRowDto MarkNorm(SpecImportNormRowDto n, Assumed assumed) =>
        n.Action == SpecImportBuilder.ActionError ? n
        : assumed.Materials.Contains(n.MaterialCode)
            ? n with
            {
                Action = SpecImportBuilder.ActionNewMaterial,
                Error = string.Format(Messages.MaterialNotInCatalog, n.MaterialCode),
                MaterialName = null,
                Unit = null,
                MaterialColor = null,
            }
        : assumed.Colors.Contains(Up(n.Color))
            ? n with { Action = SpecImportBuilder.ActionPending, Error = PendingColor([n.Color], assumed) }
        : n;

    private static string PendingColor(IEnumerable<string> colors, Assumed assumed) => string.Format(Messages.SpecImportPendingColor,
        string.Join(", ", colors.Where(c => assumed.Colors.Contains(Up(c))).Select(c => c.Trim())));

    /// <summary>Chưa có đơn: bìa của đơn đúng định dạng thì Pending, sai định dạng vẫn là Error.</summary>
    private static SpecImportOrderMarkerRowDto PendingMarker(SpecOrderMarkerExcelRow m) => new(m.RowNumber,
        m.ParseError is null ? SpecImportBuilder.ActionPending : SpecImportBuilder.ActionError,
        m.ParseError ?? Messages.SpecImportPendingMarker, m.MarkerNo, m.MarkerCode, m.LengthM, m.WidthM, m.Status, m.Ratios);
}
