using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;

namespace NX_MTD.Application.Services;

/// <summary>NVL tra theo mã khi nhập định mức (chỉ NVL đang dùng).</summary>
public sealed record SpecImportMaterial(int MaterialId, string MaterialCode, string MaterialName, string Unit, string Color);

/// <summary>
/// Dữ liệu cũ đã được dùng ở bảng khác (chế độ thay thế không được xóa hay sửa): tên thành phần (hoa) có định mức đã dùng, cặp
/// "MÀU|CHI TIẾT" có định mức đã dùng, số nhóm bìa đã có bìa của đơn.
/// </summary>
public sealed record SpecImportUsage(IReadOnlySet<string> Components, IReadOnlySet<string> NormPairs, IReadOnlySet<int> Groups)
{
    public static SpecImportUsage None { get; } = new(new HashSet<string>(), new HashSet<string>(), new HashSet<int>());
}

/// <summary>
/// DEV-ALL-115: phần tính toán thuần (không truy vấn) của nhập Excel định mức. Kiểm từng dòng file so với dữ liệu hệ thống, đánh dấu
/// Thêm mới / Cập nhật / Không đổi / Lỗi, rồi GỘP các dòng hợp lệ vào định mức và bìa đang có: dòng trùng khóa thì ghi đè, dòng không
/// có trong file giữ nguyên. Khóa: thành phần = tên; định mức = màu + chi tiết (thay cả bộ NVL của cặp đó); nhóm bìa = số nhóm;
/// bìa của đơn = nhóm + mã bìa. Kết quả gộp là đúng các DTO của nút Lưu, nên mọi kiểm tra của nút Lưu dùng lại được.
/// </summary>
public sealed class SpecImportBuilder
{
    public const string ActionNew = "New";
    public const string ActionUpdate = "Update";
    public const string ActionUnchanged = "Unchanged";
    public const string ActionError = "Error";
    /// <summary>DEV-ALL-122 (chỉ ở popup kiểm tra Excel): dòng đúng định dạng nhưng cần màu / đơn chưa có, kiểm khi Lưu đơn.</summary>
    public const string ActionPending = "Pending";
    /// <summary>DEV-ALL-123 (chỉ ở popup kiểm tra Excel): mã NVL chưa có trong danh mục — cảnh báo, admin chọn thêm vào danh mục.</summary>
    public const string ActionNewMaterial = "NewMaterial";
    /// <summary>Size của biến thể tạm: màu đã có ở mã hàng (ProductColor) nhưng chưa có size từ đơn hàng.</summary>
    public const string ColorOnlySize = "?";
    private const int MaxNameLength = 100;
    private const int MaxNoteLength = 200;
    private const short MaxPieces = 99;

    private readonly ProductSpecDto _spec;
    private readonly IReadOnlyDictionary<string, SpecImportMaterial> _materials;
    private readonly OrderMarkerProductDto? _orderProduct;
    private readonly string _productCode;
    private readonly Dictionary<string, string> _colors;                       // MÀU → tên màu trong hệ thống (biến thể đang dùng)
    private readonly Dictionary<string, ProductComponentDto> _components;      // TÊN → thành phần đang dùng
    private readonly Dictionary<string, (string Name, string Type, short Pieces)> _validComponents = new();
    private readonly List<SaveProductNormDto> _validNorms = [];
    private readonly Dictionary<int, SpecMarkerGroupExcelRow> _validGroups = new();
    private readonly List<string> _notes = [];
    private int _duplicates;
    private readonly bool _replaceComponents, _replaceNorms, _replaceGroups, _replaceMarkers;
    private readonly SpecImportUsage _usage;
    private readonly HashSet<string> _fileComponentKeys, _fileNormKeys;
    private readonly HashSet<int> _fileGroupNos;
    private readonly HashSet<(int No, string Code)> _fileMarkerKeys;

    /// <param name="replace">
    /// Chế độ thay thế: bảng nào có trong file thì dữ liệu cũ của bảng đó không có trong file bị xóa, TRỪ dữ liệu đã được dùng ở bảng
    /// khác (usage): dữ liệu đã dùng giữ nguyên, không xóa, không ngừng dùng. Bảng không có trong file không bao giờ bị đụng tới.
    /// Dòng lỗi vẫn giữ dữ liệu cũ cùng khóa.
    /// </param>
    public SpecImportBuilder(ProductSpecDto spec, IReadOnlyDictionary<string, SpecImportMaterial> materials,
        OrderMarkerProductDto? orderProduct, string productCode, SpecWorkbookDto workbook, bool replace = false,
        SpecImportUsage? usage = null)
    {
        _usage = usage ?? SpecImportUsage.None;
        _replaceComponents = replace && workbook.HasComponents;
        _replaceNorms = replace && workbook.HasNorms;
        _replaceGroups = replace && workbook.HasMarkerGroups;
        _replaceMarkers = replace && workbook.HasOrderMarkers && orderProduct is not null;
        _fileComponentKeys = workbook.Components.Select(r => Upper(r.Name)).ToHashSet();
        _fileNormKeys = workbook.Norms.Select(r => $"{Upper(r.Color)}|{Upper(r.ComponentName)}").ToHashSet();
        _fileGroupNos = workbook.MarkerGroups.Where(r => r.MarkerNo is not null).Select(r => r.MarkerNo!.Value).ToHashSet();
        _fileMarkerKeys = workbook.OrderMarkers.Where(r => r.MarkerNo is not null).Select(r => (r.MarkerNo!.Value, Upper(r.MarkerCode))).ToHashSet();

        _spec = spec;
        _materials = materials;
        _orderProduct = orderProduct;
        _productCode = productCode;
        _colors = spec.Variants.Where(v => v.Status == "Active").GroupBy(v => Upper(v.Color))
            .ToDictionary(g => g.Key, g => g.First().Color.Trim());
        _components = spec.Components.Where(c => c.Status == "Active").ToDictionary(c => Upper(c.ComponentName));

        Components = BuildComponents(workbook.Components);
        Norms = BuildNorms(workbook.Norms);
        MarkerGroups = BuildGroups(workbook.MarkerGroups);
        Spec = MergeSpec();
        if (workbook.HasOrderMarkers && orderProduct is null) _notes.Add(Messages.SpecImportMarkersSkipped);
        OrderMarkers = orderProduct is not null && workbook.HasOrderMarkers ? BuildOrderMarkers(workbook) : [];
        Markers = orderProduct is not null && workbook.HasOrderMarkers ? MergeMarkers() : null;
        ComputeRemoved();
    }

    public IReadOnlyList<SpecImportComponentRowDto> Components { get; }
    public IReadOnlyList<SpecImportNormRowDto> Norms { get; }
    public IReadOnlyList<SpecImportMarkerGroupRowDto> MarkerGroups { get; }
    public IReadOnlyList<SpecImportOrderMarkerRowDto> OrderMarkers { get; private set; } = [];
    /// <summary>Định mức sau khi gộp (đủ danh sách như nút Lưu gửi).</summary>
    public SaveProductSpecDto Spec { get; }
    /// <summary>Bìa của mã hàng trong đơn sau khi gộp; null khi không nhập bìa của đơn.</summary>
    public SaveOrderMarkersDto? Markers { get; }
    public IReadOnlyList<string> Notes => _notes;
    public int DuplicateCount => _duplicates;

    public SpecImportResultDto Changes => new(Changed(Components.Select(r => r.Action)), Changed(Norms.Select(r => r.Action)),
        Changed(MarkerGroups.Select(r => r.Action)), Changed(OrderMarkers.Select(r => r.Action)));

    public bool SpecChanged => Changes is { } c && c.Components + c.Norms + c.MarkerGroups > 0 || RemovedSpecCount > 0;

    /// <summary>Dòng cũ bị bỏ vì không có trong file (chỉ khi chế độ thay thế): thành phần, cặp màu + chi tiết của định mức, nhóm bìa, bìa của đơn.</summary>
    public SpecImportResultDto Removed { get; private set; } = new(0, 0, 0, 0);
    public IReadOnlyList<string> RemovedNames { get; private set; } = [];
    public int RemovedSpecCount => Removed.Components + Removed.Norms + Removed.MarkerGroups;
    public int RemovedMarkers => Removed.OrderMarkers;
    public int RemovedTotal => RemovedSpecCount + RemovedMarkers;

    public IEnumerable<string> AllActions => Components.Select(r => r.Action).Concat(Norms.Select(r => r.Action))
        .Concat(MarkerGroups.Select(r => r.Action)).Concat(OrderMarkers.Select(r => r.Action));

    private static int Changed(IEnumerable<string> actions) => actions.Count(a => a is ActionNew or ActionUpdate);

    // ---------- Thành phần ----------

    private List<SpecImportComponentRowDto> BuildComponents(IReadOnlyList<SpecComponentExcelRow> rows)
    {
        var seen = new Dictionary<string, int>();
        var result = new List<SpecImportComponentRowDto>(rows.Count);
        foreach (var row in rows)
        {
            var key = Upper(row.Name);
            var error = row.ParseError
                        ?? (row.Name.Length > MaxNameLength ? Messages.SpecImportNameTooLong : null)
                        ?? (row.PiecesPerGarment is null or < 1 or > MaxPieces ? Messages.SpecImportPiecesRange : null)
                        ?? Duplicate(seen, key, row.RowNumber);
            _components.TryGetValue(key, out var existing);
            var action = error is not null ? ActionError
                : existing is null ? ActionNew
                : CodeText.ToCode(existing.ComponentType) == row.ComponentType && existing.PiecesPerGarment == row.PiecesPerGarment
                    ? ActionUnchanged : ActionUpdate;
            if (error is null)
                _validComponents[key] = (existing?.ComponentName ?? row.Name, row.ComponentType!, row.PiecesPerGarment!.Value);
            result.Add(new SpecImportComponentRowDto(row.RowNumber, action, error, row.Name, row.ComponentType, row.PiecesPerGarment));
        }
        return result;
    }

    /// <summary>Loại (PANEL/TRIM) và tên chuẩn của chi tiết sau khi nhập: thành phần đang có, đè bởi dòng hợp lệ của file.</summary>
    private (string Name, string Type)? ComponentOf(string name)
    {
        var key = Upper(name);
        if (_validComponents.TryGetValue(key, out var v)) return (v.Name, v.Type);
        return _components.TryGetValue(key, out var c) ? (c.ComponentName, CodeText.ToCode(c.ComponentType)) : null;
    }

    // ---------- Định mức NVL ----------

    private List<SpecImportNormRowDto> BuildNorms(IReadOnlyList<SpecNormExcelRow> rows)
    {
        var seen = new Dictionary<string, int>();
        var checkedRows = new List<(SpecNormExcelRow Row, string? Error, string Color, string Component, SpecImportMaterial? Material)>();
        foreach (var row in rows)
        {
            var color = _colors.GetValueOrDefault(Upper(row.Color));
            var component = ComponentOf(row.ComponentName);
            var material = _materials.GetValueOrDefault(row.MaterialCode);
            var error = row.ParseError
                        ?? (color is null ? string.Format(Messages.SpecImportColorUnknown, row.Color) : null)
                        ?? (component is null ? string.Format(Messages.SpecImportComponentUnknown, row.ComponentName) : null)
                        ?? (material is null ? string.Format(Messages.SpecImportMaterialUnknown, row.MaterialCode) : null)
                        ?? (row.SpecNote is { Length: > MaxNoteLength } ? Messages.SpecImportNoteTooLong : null)
                        ?? Duplicate(seen, $"{Upper(row.Color)}|{Upper(row.ComponentName)}|{row.MaterialCode}", row.RowNumber);
            checkedRows.Add((row, error, color ?? row.Color, component?.Name ?? row.ComponentName, material));
            if (error is null) _validNorms.Add(new SaveProductNormDto(component!.Value.Name, color!, material!.MaterialId, row.SpecNote));
        }

        return checkedRows.Select(c => new SpecImportNormRowDto(c.Row.RowNumber,
            c.Error is not null ? ActionError : NormAction(c.Color, c.Component), c.Error, c.Color, c.Component,
            c.Row.MaterialCode, c.Material?.MaterialName, c.Material?.Unit, c.Material?.Color, c.Row.SpecNote)).ToList();
    }

    /// <summary>So bộ NVL (kèm ghi chú) của cặp màu + chi tiết trong file với bộ đang có.</summary>
    private string NormAction(string color, string component)
    {
        static string Sig(IEnumerable<(int MaterialId, string? Note)> items) =>
            string.Join(';', items.Select(i => $"{i.MaterialId}:{i.Note?.Trim()}").Order(StringComparer.Ordinal));

        var current = _spec.Norms.Where(n => Same(n.Color, color) && Same(n.ComponentName, component)).ToList();
        if (current.Count == 0) return ActionNew;
        var file = _validNorms.Where(n => Same(n.Color, color) && Same(n.ComponentName, component));
        return Sig(current.Select(n => (n.MaterialId, n.SpecNote))) == Sig(file.Select(n => (n.MaterialId, n.SpecNote)))
            ? ActionUnchanged : ActionUpdate;
    }

    // ---------- Nhóm bìa (bìa loại 1) ----------

    private List<SpecImportMarkerGroupRowDto> BuildGroups(IReadOnlyList<SpecMarkerGroupExcelRow> rows)
    {
        var seen = new Dictionary<string, int>();
        var result = new List<SpecImportMarkerGroupRowDto>(rows.Count);
        foreach (var row in rows)
        {
            // DEV-ALL-121: màu chưa có ở mã hàng thì bỏ qua màu đó (ghi chú), như định mức NVL bỏ qua dòng màu lạ; chỉ lỗi khi
            // không còn màu nào. Một file dùng chung cho nhiều đơn khai sẵn mọi màu, đơn chỉ đặt một phần.
            var unknownColors = row.Colors.Where(c => !_colors.ContainsKey(Upper(c))).ToList();
            var knownColors = row.Colors.Where(c => _colors.ContainsKey(Upper(c))).ToList();
            var error = row.ParseError
                        ?? (row.MarkerNo > SaveProductSpecDtoValidator.MaxMarkerNo
                            ? string.Format(Messages.SpecImportMarkerNoRange, SaveProductSpecDtoValidator.MaxMarkerNo) : null)
                        ?? GroupComponentProblem(row.ComponentNames)
                        ?? (knownColors.Count == 0 && unknownColors.Count > 0
                            ? string.Format(Messages.SpecImportColorUnknown, unknownColors[0]) : null)
                        ?? Duplicate(seen, row.MarkerNo?.ToString() ?? string.Empty, row.RowNumber);
            if (error is null && unknownColors.Count > 0)
                _notes.Add(string.Format(Messages.SpecImportGroupColorsSkipped, row.MarkerNo, string.Join(", ", unknownColors)));
            var components = row.ComponentNames.Select(n => ComponentOf(n)?.Name ?? n).ToList();
            var colors = (error is null ? knownColors : row.Colors).Select(c => _colors.GetValueOrDefault(Upper(c)) ?? c).ToList();
            if (error is null) _validGroups[row.MarkerNo!.Value] = row with { ComponentNames = components, Colors = colors };
            result.Add(new SpecImportMarkerGroupRowDto(row.RowNumber, error is null ? GroupAction(row.MarkerNo!.Value, components, colors)
                : ActionError, error, row.MarkerNo, components, colors));
        }
        return result;
    }

    private string? GroupComponentProblem(IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            var component = ComponentOf(name);
            if (component is null) return string.Format(Messages.SpecImportComponentUnknown, name);
            if (component.Value.Type != ComponentTypes.Panel) return string.Format(Messages.SpecImportComponentNotPanel, name);
        }
        return null;
    }

    private string GroupAction(int markerNo, IReadOnlyList<string> components, IReadOnlyList<string> colors)
    {
        var currentComponents = _spec.MarkerComponents.Where(m => m.MarkerNo == markerNo).Select(m => Upper(m.ComponentName)).ToHashSet();
        var currentColors = _spec.MarkerFabrics.Where(m => m.MarkerNo == markerNo).Select(m => Upper(m.Color)).ToHashSet();
        if (currentComponents.Count == 0 && currentColors.Count == 0) return ActionNew;
        return currentComponents.SetEquals(components.Select(Upper)) && currentColors.SetEquals(colors.Select(Upper))
            ? ActionUnchanged : ActionUpdate;
    }

    // ---------- Gộp định mức ----------

    /// <summary>Thay thế: thành phần đã có định mức được dùng, hoặc nằm trên nhóm bìa đã có bìa của đơn, thì không được bỏ.</summary>
    private bool IsComponentKept(string name)
    {
        var key = Upper(name);
        return _usage.Components.Contains(key)
               || _spec.MarkerComponents.Any(m => _usage.Groups.Contains(m.MarkerNo) && Upper(m.ComponentName) == key);
    }

    private SaveProductSpecDto MergeSpec()
    {
        var components = _components.Values.Where(c => !_replaceComponents || _fileComponentKeys.Contains(Upper(c.ComponentName))
                                                       || IsComponentKept(c.ComponentName))
            .Select(c => _validComponents.TryGetValue(Upper(c.ComponentName), out var v)
                ? new SaveProductComponentDto(c.ProductComponentId, c.ComponentName, CodeText.ToPascal(v.Type), v.Pieces)
                : new SaveProductComponentDto(c.ProductComponentId, c.ComponentName, c.ComponentType, c.PiecesPerGarment))
            .Concat(_validComponents.Where(v => !_components.ContainsKey(v.Key))
                .Select(v => new SaveProductComponentDto(null, v.Value.Name, CodeText.ToPascal(v.Value.Type), v.Value.Pieces)))
            .ToList();
        var activeNames = components.Select(c => Upper(c.ComponentName)).ToHashSet();

        // Biến thể tạm (màu chưa có size, xem ColorOnlySize) chỉ để nhận màu, không ghi.
        var variants = _spec.Variants.Where(v => v.Status == "Active" && v.SizeCode != ColorOnlySize)
            .Select(v => new SaveProductVariantDto(v.ProductVariantId, v.Color, v.SizeCode)).ToList();

        var fileNormKeys = _validNorms.Select(n => (Upper(n.Color), Upper(n.ComponentName))).ToHashSet();
        var norms = _spec.Norms
            .Where(n => activeNames.Contains(Upper(n.ComponentName)) && _colors.ContainsKey(Upper(n.Color))
                        && !fileNormKeys.Contains((Upper(n.Color), Upper(n.ComponentName)))
                        && (!_replaceNorms || _fileNormKeys.Contains($"{Upper(n.Color)}|{Upper(n.ComponentName)}")
                            || _usage.NormPairs.Contains($"{Upper(n.Color)}|{Upper(n.ComponentName)}")))
            .Select(n => new SaveProductNormDto(n.ComponentName, n.Color, n.MaterialId, n.SpecNote))
            .Concat(_validNorms).ToList();

        var groupNos = _validGroups.Keys.ToHashSet();
        var markerComponents = _spec.MarkerComponents
            .Where(m => !groupNos.Contains(m.MarkerNo) && activeNames.Contains(Upper(m.ComponentName))
                        && (!_replaceGroups || _fileGroupNos.Contains(m.MarkerNo) || _usage.Groups.Contains(m.MarkerNo)))
            .Select(m => new SaveProductMarkerComponentDto(m.MarkerNo, m.ComponentName))
            .Concat(_validGroups.SelectMany(g => g.Value.ComponentNames.Select(n => new SaveProductMarkerComponentDto(g.Key, n))))
            .ToList();
        var markerColors = _spec.MarkerFabrics
            .Where(m => !groupNos.Contains(m.MarkerNo) && _colors.ContainsKey(Upper(m.Color))
                        && (!_replaceGroups || _fileGroupNos.Contains(m.MarkerNo) || _usage.Groups.Contains(m.MarkerNo)))
            .Select(m => new SaveProductMarkerColorDto(m.MarkerNo, m.Color))
            .Concat(_validGroups.SelectMany(g => g.Value.Colors.Select(c => new SaveProductMarkerColorDto(g.Key, c))))
            .ToList();
        return new SaveProductSpecDto(components, variants, norms, markerComponents, markerColors);
    }

    // ---------- Bìa của đơn (bìa loại 2) ----------

    private readonly List<SaveOrderMarkerDto> _validMarkers = [];

    private List<SpecImportOrderMarkerRowDto> BuildOrderMarkers(SpecWorkbookDto workbook)
    {
        var product = _orderProduct!;
        var sizes = product.Sizes.Select(Upper).ToHashSet();
        var ignored = workbook.SizeColumns.Where(s => !sizes.Contains(Upper(s))).ToList();
        if (ignored.Count > 0) _notes.Add(string.Format(Messages.SpecImportSizesIgnored, string.Join(", ", ignored)));
        var groups = Spec.MarkerColors.Select(c => c.MarkerNo).ToHashSet();
        var validator = new SaveOrderMarkerDtoValidator();

        var seen = new Dictionary<string, int>();
        var result = new List<SpecImportOrderMarkerRowDto>();
        var otherProduct = 0;
        foreach (var row in workbook.OrderMarkers)
        {
            if (row.ProductCode is not null && !Same(row.ProductCode, _productCode))
            {
                otherProduct++;
                continue;
            }
            var ratios = row.Ratios.Where(r => sizes.Contains(Upper(r.SizeCode))).ToList();
            var existing = product.Markers.FirstOrDefault(m => m.MarkerNo == row.MarkerNo && Same(m.MarkerCode, row.MarkerCode));
            var input = new SaveOrderMarkerDto(existing?.ProductMarkerId, row.MarkerNo ?? 0, row.MarkerCode, row.LengthM ?? 0,
                row.WidthM, CodeText.ToPascal(row.Status), ratios);
            var error = row.ParseError
                        ?? (row.WidthM is null ? Messages.Required("khổ bìa") : null)
                        ?? (ratios.Count == 0 ? Messages.SpecImportNoSizeOfOrder : null)
                        ?? validator.Validate(input).Errors.Select(e => e.ErrorMessage).FirstOrDefault()
                        ?? (groups.Contains(input.MarkerNo) ? null : string.Format(Messages.SpecImportGroupUnknown, input.MarkerNo))
                        ?? Duplicate(seen, $"{input.MarkerNo}|{Upper(row.MarkerCode)}", row.RowNumber);
            if (error is null) _validMarkers.Add(input);
            var action = error is not null ? ActionError : existing is null ? ActionNew : SameMarker(existing, input) ? ActionUnchanged : ActionUpdate;
            result.Add(new SpecImportOrderMarkerRowDto(row.RowNumber, action, error, row.MarkerNo, row.MarkerCode.ToUpperInvariant(),
                row.LengthM, row.WidthM, CodeText.ToPascal(row.Status), ratios));
        }
        if (otherProduct > 0) _notes.Add(string.Format(Messages.SpecImportOtherProductRows, otherProduct));
        return result;
    }

    private static bool SameMarker(OrderMarkerDto current, SaveOrderMarkerDto input) =>
        current.LengthM == input.LengthM && current.WidthM == input.WidthM && Same(current.Status, input.Status)
        && string.Join(';', current.Ratios.OrderBy(r => Upper(r.SizeCode)).Select(r => $"{Upper(r.SizeCode)}:{r.PiecesPerPly}"))
        == string.Join(';', input.Ratios.OrderBy(r => Upper(r.SizeCode)).Select(r => $"{Upper(r.SizeCode)}:{r.PiecesPerPly}"));

    private SaveOrderMarkersDto MergeMarkers()
    {
        var replaced = _validMarkers.Where(m => m.ProductMarkerId is not null).Select(m => m.ProductMarkerId!.Value).ToHashSet();
        var kept = _orderProduct!.Markers.Where(m => !replaced.Contains(m.ProductMarkerId)
                && (!_replaceMarkers || _fileMarkerKeys.Contains((m.MarkerNo, Upper(m.MarkerCode)))))
            .Select(m => new SaveOrderMarkerDto(m.ProductMarkerId, m.MarkerNo, m.MarkerCode, m.LengthM, m.WidthM, m.Status, m.Ratios));
        return new SaveOrderMarkersDto(kept.Concat(_validMarkers).ToList());
    }

    // ---------- Dòng cũ bị bỏ (chế độ thay thế) ----------

    private void ComputeRemoved()
    {
        if (!(_replaceComponents || _replaceNorms || _replaceGroups || _replaceMarkers)) return;   // không thay thế: không có dòng nào bị bỏ
        var names = new List<string>();
        var mergedComponents = Spec.Components.Select(c => Upper(c.ComponentName)).ToHashSet();
        var removedComponents = _components.Values.Where(c => _replaceComponents && !mergedComponents.Contains(Upper(c.ComponentName))).ToList();
        names.AddRange(removedComponents.Select(c => $"Thành phần: {c.ComponentName}"));

        var mergedPairs = Spec.Norms.Select(n => $"{Upper(n.Color)}|{Upper(n.ComponentName)}").ToHashSet();
        var removedPairs = _spec.Norms
            .Where(n => _replaceNorms && mergedComponents.Contains(Upper(n.ComponentName)) && _colors.ContainsKey(Upper(n.Color)))
            .Select(n => (Key: $"{Upper(n.Color)}|{Upper(n.ComponentName)}", n.Color, n.ComponentName))
            .Where(p => !mergedPairs.Contains(p.Key)).DistinctBy(p => p.Key).ToList();
        names.AddRange(removedPairs.Select(p => $"Định mức: {p.Color} · {p.ComponentName}"));

        var mergedGroupNos = Spec.MarkerComponents.Select(m => m.MarkerNo).Concat(Spec.MarkerColors.Select(m => m.MarkerNo)).ToHashSet();
        var removedGroups = _spec.MarkerComponents.Select(m => m.MarkerNo).Concat(_spec.MarkerFabrics.Select(m => m.MarkerNo))
            .Distinct().Where(no => _replaceGroups && !mergedGroupNos.Contains(no)).Order().ToList();
        names.AddRange(removedGroups.Select(no => $"Nhóm bìa: {no}"));

        var removedMarkers = _orderProduct is null || Markers is null ? new List<OrderMarkerDto>()
            : _orderProduct.Markers.Where(m => _replaceMarkers && Markers.Markers.All(k => k.ProductMarkerId != m.ProductMarkerId)).ToList();
        names.AddRange(removedMarkers.Select(m => $"Bìa của đơn: {m.MarkerCode}"));

        Removed = new SpecImportResultDto(removedComponents.Count, removedPairs.Count, removedGroups.Count, removedMarkers.Count);
        RemovedNames = names;
        KeptUsedNames = KeptUsed(mergedComponents, mergedPairs, mergedGroupNos);
    }

    /// <summary>Thay thế: dòng cũ không có trong file nhưng đã được dùng nên được giữ nguyên (báo lại cho người dùng).</summary>
    public IReadOnlyList<string> KeptUsedNames { get; private set; } = [];

    private List<string> KeptUsed(HashSet<string> mergedComponents, HashSet<string> mergedPairs, HashSet<int> mergedGroupNos)
    {
        var names = new List<string>();
        names.AddRange(_components.Values.Where(c => _replaceComponents && !_fileComponentKeys.Contains(Upper(c.ComponentName))
                && mergedComponents.Contains(Upper(c.ComponentName))).Select(c => $"Thành phần: {c.ComponentName}"));
        names.AddRange(_spec.Norms.Where(n => _replaceNorms && !_fileNormKeys.Contains($"{Upper(n.Color)}|{Upper(n.ComponentName)}")
                && mergedPairs.Contains($"{Upper(n.Color)}|{Upper(n.ComponentName)}"))
            .DistinctBy(n => $"{Upper(n.Color)}|{Upper(n.ComponentName)}").Select(n => $"Định mức: {n.Color} · {n.ComponentName}"));
        names.AddRange(mergedGroupNos.Where(no => _replaceGroups && !_fileGroupNos.Contains(no)).Order().Select(no => $"Nhóm bìa: {no}"));
        return names;
    }

    // ---------- Tiện ích ----------

    private string? Duplicate(Dictionary<string, int> seen, string key, int rowNumber)
    {
        if (seen.TryGetValue(key, out var first))
        {
            _duplicates++;
            return string.Format(Messages.SpecImportDuplicateRow, first);
        }
        seen[key] = rowNumber;
        return null;
    }

    private static string Upper(string value) => value.Trim().ToUpperInvariant();
    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
