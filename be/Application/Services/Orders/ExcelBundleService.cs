using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-122: popup kiểm tra file Excel nhiều sheet ở form mã hàng. Đọc mọi bảng của file một lần (Mã hàng, Thành phần, Định mức
/// NVL, Nhóm bìa, Bìa của đơn, NVL của đơn) để màn hình sửa; kiểm bản đã sửa bằng đúng logic xem trước đang có. Không ghi gì: dữ
/// liệu chỉ được ghi khi bấm Lưu (gọi các API nhập "-data").
/// </summary>
public sealed class ExcelBundleService : IExcelBundleService
{
    private const string ActionError = "Error";

    private readonly IUnitOfWork _uow;
    private readonly IExcelProductParser _products;
    private readonly IExcelSpecWorkbookParser _spec;
    private readonly IExcelOrderMaterialParser _orderMaterials;
    private readonly IExcelMaterialParser _materials;
    private readonly IProductImportService _productImport;
    private readonly ISpecImportService _specImport;
    private readonly IOrderMaterialService _orderMaterialService;

    public ExcelBundleService(IUnitOfWork uow, IExcelProductParser products, IExcelSpecWorkbookParser spec,
        IExcelOrderMaterialParser orderMaterials, IExcelMaterialParser materials, IProductImportService productImport,
        ISpecImportService specImport, IOrderMaterialService orderMaterialService)
    {
        _uow = uow; _products = products; _spec = spec; _orderMaterials = orderMaterials; _materials = materials;
        _productImport = productImport; _specImport = specImport; _orderMaterialService = orderMaterialService;
    }

    private static readonly SpecWorkbookDto EmptySpec = new(false, [], false, [], false, [], false, [], []);

    public Result<ExcelBundleDto> Parse(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        // Mỗi parser tự tìm bảng của mình; bảng không có (hoặc file không phải Excel) thì coi như rỗng.
        T? Read<T>(Func<Stream, T> parse) where T : class
        {
            buffer.Position = 0;
            try
            {
                return parse(buffer);
            }
            catch (Exception ex) when (ex is InvalidDataException or ExcelTableMissingException or ArgumentException
                                           or FormatException or OverflowException or InvalidOperationException)
            {
                return null;
            }
        }

        var products = Read(_products.Parse);
        var spec = Read(_spec.Parse);
        var orderMaterials = Read(_orderMaterials.Parse);
        var materials = Read(_materials.Parse);
        if (products is null && spec is null && orderMaterials is null && materials is null)
            return Result<ExcelBundleDto>.Failure(ErrorCodes.SpecImportInvalid, Messages.ExcelBundleEmpty, ResultKind.Validation);

        return Result<ExcelBundleDto>.Success(new ExcelBundleDto(
            products is not null, products ?? [], spec ?? EmptySpec, orderMaterials is not null, orderMaterials ?? [],
            materials is not null, materials ?? []));
    }

    public async Task<Result<ExcelBundleCheckResultDto>> CheckAsync(ExcelBundleCheckDto dto, CancellationToken ct)
    {
        var bundle = dto.Bundle;
        ProductImportPreviewDto? products = null;
        if (bundle.HasProducts)
        {
            var p = await _productImport.PreviewAsync(bundle.Products, ct);
            if (!p.IsSuccess) return Fail.As<ExcelBundleCheckResultDto>(p);
            products = p.Data;
        }

        var spec = await _specImport.CheckAsync(dto.ProductId, dto.OrderId, bundle.Spec, ct, dto.Replace);
        if (!spec.IsSuccess) return Fail.As<ExcelBundleCheckResultDto>(spec);

        OrderMaterialImportPreviewDto? orderMaterials = null;
        var catalog = await CatalogAsync(bundle.OrderMaterials.Select(r => r.MaterialCode), ct);
        if (bundle.HasOrderMaterials)
        {
            if (dto.OrderId is { } orderId)
            {
                var m = await _orderMaterialService.PreviewImportAsync(orderId, bundle.OrderMaterials, ct, dto.Replace);
                if (!m.IsSuccess) return Fail.As<ExcelBundleCheckResultDto>(m);
                orderMaterials = MarkNewMaterials(m.Data!, catalog, bundle.OrderMaterials);
            }
            else orderMaterials = await CheckOrderMaterialsWithoutOrderAsync(bundle.OrderMaterials, catalog, ct);
        }

        var sheetRows = bundle.HasMaterials ? bundle.Materials ?? [] : [];
        var sheet = await new MaterialSheetChecker(_uow).CheckAsync(sheetRows, ct);
        var missing = MissingMaterials(bundle, spec.Data!, orderMaterials, sheet);
        // NVL đã khai đủ ở sheet NVL thì không còn là cảnh báo "NVL chưa có": chỉ NVL dùng ở định mức / NVL của đơn mà sheet NVL không có mới cảnh báo.
        var declared = missing.Where(m => m.FromSheet).Select(m => m.MaterialCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var specData = spec.Data! with { Norms = spec.Data!.Norms.Select(n => IsDeclared(n.Action, n.MaterialCode, declared) ? n with { Action = SpecImportBuilder.ActionNew, Error = null } : n).ToList() };
        orderMaterials = orderMaterials is null ? null : orderMaterials with
        {
            Rows = orderMaterials.Rows.Select(r => IsDeclared(r.Action, r.MaterialCode, declared) ? r with { Action = SpecImportBuilder.ActionNew, Error = null } : r).ToList(),
        };
        return Result<ExcelBundleCheckResultDto>.Success(new ExcelBundleCheckResultDto(products, specData, orderMaterials, missing, sheet));
    }

    private static bool IsDeclared(string action, string code, HashSet<string> declared) =>
        action == SpecImportBuilder.ActionNewMaterial && declared.Contains(code.Trim());

    /// <summary>Mã NVL có trong danh mục, kể cả đã ngừng dùng (không phân biệt hoa thường).</summary>
    private async Task<HashSet<string>> CatalogAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        var wanted = codes.Where(c => c.Length > 0).Distinct().ToList();
        var rows = await _uow.Repository<Material>().Query().AsNoTracking()
            .Where(m => wanted.Contains(m.MaterialCode)).Select(m => m.MaterialCode).ToListAsync(ct);
        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>NVL mới sẽ được tạo từ dòng này: tên / đơn vị gợi ý không được vượt giới hạn của danh mục NVL.</summary>
    private static string? NewMaterialInfoProblem(OrderMaterialExcelRow row) =>
        row.MaterialName?.Trim().Length > MaterialLimits.MaxNameLength ? Messages.SpecImportMaterialNameTooLong
        : row.Unit?.Trim().Length > 20 ? Messages.SpecImportMaterialUnitTooLong
        : null;

    /// <summary>Có đơn: dòng lỗi chỉ vì mã NVL chưa có trong danh mục thì đổi thành cảnh báo NewMaterial (mã sai định dạng, tên / đơn vị quá dài vẫn là lỗi).</summary>
    private static OrderMaterialImportPreviewDto MarkNewMaterials(OrderMaterialImportPreviewDto preview, HashSet<string> catalog,
        IReadOnlyList<OrderMaterialExcelRow> fileRows)
    {
        var rows = preview.Rows.Select(r =>
        {
            if (r.Action != ActionError || r.MaterialName is not null || r.MaterialCode.Length == 0 || catalog.Contains(r.MaterialCode)) return r;
            var fileRow = fileRows.FirstOrDefault(f => f.RowNumber == r.RowNumber);
            var problem = SpecImportBuilder.MaterialCodeProblem(r.MaterialCode) ?? (fileRow is null ? null : NewMaterialInfoProblem(fileRow));
            return problem is not null ? r with { Error = problem }
                : r with { Action = SpecImportBuilder.ActionNewMaterial, Error = string.Format(Messages.MaterialNotInCatalog, r.MaterialCode) };
        }).ToList();
        var errors = rows.Count(r => r.Action == ActionError);
        return preview with { Rows = rows, ValidCount = rows.Count - errors, ErrorCount = errors - preview.DuplicateCount };
    }

    /// <summary>Gom mã NVL chưa có (định mức + NVL của đơn), gợi ý loại theo chi tiết PANEL và màu áo đang dùng mã đó.</summary>
    private static List<MissingMaterialDto> MissingMaterials(ExcelBundleDto bundle, SpecImportPreviewDto spec,
        OrderMaterialImportPreviewDto? orderMaterials, IReadOnlyList<MaterialCheckRowDto> sheet)
    {
        var panels = bundle.Spec.Components.Where(c => c.ComponentType == ComponentTypes.Panel)
            .Select(c => c.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normRows = spec.Norms.Where(n => n.Action == SpecImportBuilder.ActionNewMaterial).ToList();
        // DEV-ALL-192: dòng sheet NVL hợp lệ mà mã chưa có trong danh mục cũng được thêm (đủ thông tin, không cần admin điền).
        var sheetNew = (bundle.Materials ?? []).Where(r => sheet.Any(s => s.RowNumber == r.RowNumber && s.Action == SpecImportBuilder.ActionNew))
            .GroupBy(r => r.MaterialCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var codes = normRows.Select(n => n.MaterialCode)
            .Concat(orderMaterials?.Rows.Where(r => r.Action == SpecImportBuilder.ActionNewMaterial).Select(r => r.MaterialCode) ?? [])
            .Concat(sheetNew.Keys)
            .Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
        return codes.Select(code =>
        {
            var uses = normRows.Where(n => string.Equals(n.MaterialCode, code, StringComparison.OrdinalIgnoreCase)).ToList();
            var fabric = uses.Any(n => panels.Contains(n.ComponentName.Trim()));
            // Tên, đơn vị gợi ý lấy từ cột Tên NVL / Đơn vị của sheet NVL của đơn (nếu file có).
            var fileRow = bundle.OrderMaterials.FirstOrDefault(r =>
                string.Equals(r.MaterialCode.Trim(), code, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(r.MaterialName));
            var colors = uses.Select(n => n.Color.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (sheetNew.TryGetValue(code, out var row))
                return new MissingMaterialDto(code, row.MaterialType ?? MaterialTypes.Trim, colors, row.MaterialName, row.Unit, row.WidthM,
                    row.Color ?? string.Empty, FromSheet: true);
            return new MissingMaterialDto(code, fabric ? MaterialTypes.Fabric : MaterialTypes.Trim, colors, fileRow?.MaterialName, fileRow?.Unit);
        }).ToList();
    }

    /// <summary>Chưa có đơn: kiểm định dạng, mã NVL có trong danh mục, số lượng, trùng dòng; phần theo đơn để Pending.</summary>
    private async Task<OrderMaterialImportPreviewDto> CheckOrderMaterialsWithoutOrderAsync(IReadOnlyList<OrderMaterialExcelRow> rows,
        HashSet<string> catalog, CancellationToken ct)
    {
        var codes = rows.Select(r => r.MaterialCode).Where(c => c.Length > 0).Distinct().ToList();
        var materials = (await _uow.Repository<Material>().Query().AsNoTracking()
                .Where(m => codes.Contains(m.MaterialCode) && m.Status == MaterialStatuses.Active).ToListAsync(ct))
            .GroupBy(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = 0;
        var result = new List<OrderMaterialImportRowDto>(rows.Count);
        var validator = new OrderMaterialInputValidator();
        foreach (var row in rows)
        {
            materials.TryGetValue(row.MaterialCode, out var material);
            string? duplicate = null;
            if (row.ParseError is null && seen.TryGetValue(row.MaterialCode, out var first))
            {
                duplicate = string.Format(Messages.SpecImportDuplicateRow, first);
                duplicates++;
            }
            else if (row.ParseError is null) seen[row.MaterialCode] = row.RowNumber;

            // NVL chưa có trong danh mục: kiểm số lượng như NVL có thật (Id tạm 1), dòng hợp lệ thì là cảnh báo NewMaterial.
            var codeProblem = material is null ? SpecImportBuilder.MaterialCodeProblem(row.MaterialCode) : null;
            var isNew = material is null && row.MaterialCode.Length > 0 && !catalog.Contains(row.MaterialCode) && codeProblem is null;
            var input = new CreateOrderMaterialLineDto(material?.MaterialId ?? (isNew ? 1 : 0), row.RequiredQty ?? 0, row.CustomerQty ?? 0,
                row.Note);
            var error = row.ParseError ?? duplicate
                        ?? codeProblem
                        ?? (material is null && !isNew ? (catalog.Contains(row.MaterialCode) ? WarehouseMessages.MaterialInactive : string.Format(WarehouseMessages.OrderMaterialImportUnknown, row.MaterialCode)) : null)
                        ?? (isNew ? NewMaterialInfoProblem(row) : null)
                        ?? validator.Validate(input).Errors.Select(e => e.ErrorMessage).FirstOrDefault();
            var action = error is not null ? ActionError : isNew ? SpecImportBuilder.ActionNewMaterial : SpecImportBuilder.ActionPending;
            var note = error ?? (isNew ? string.Format(Messages.MaterialNotInCatalog, row.MaterialCode) : Messages.OrderMaterialPendingOrder);
            result.Add(new OrderMaterialImportRowDto(row.RowNumber, action,
                note, row.MaterialCode, material?.MaterialName, material?.Color, material?.Unit,
                row.RequiredQty, row.CustomerQty,
                row.RequiredQty is { } r && row.CustomerQty is { } c ? Math.Max(r - c, 0) : null, row.Note));
        }
        var errors = result.Count(r => r.Action == ActionError);
        return new OrderMaterialImportPreviewDto(result, result.Count - errors, errors - duplicates, duplicates, 0);
    }
}
