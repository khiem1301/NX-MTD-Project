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
    private readonly IProductImportService _productImport;
    private readonly ISpecImportService _specImport;
    private readonly IOrderMaterialService _orderMaterialService;

    public ExcelBundleService(IUnitOfWork uow, IExcelProductParser products, IExcelSpecWorkbookParser spec,
        IExcelOrderMaterialParser orderMaterials, IProductImportService productImport, ISpecImportService specImport,
        IOrderMaterialService orderMaterialService)
    {
        _uow = uow; _products = products; _spec = spec; _orderMaterials = orderMaterials;
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
        if (products is null && spec is null && orderMaterials is null)
            return Result<ExcelBundleDto>.Failure(ErrorCodes.SpecImportInvalid, Messages.ExcelBundleEmpty, ResultKind.Validation);

        return Result<ExcelBundleDto>.Success(new ExcelBundleDto(
            products is not null, products ?? [], spec ?? EmptySpec, orderMaterials is not null, orderMaterials ?? []));
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

        var spec = await _specImport.CheckAsync(dto.ProductId, dto.OrderId, bundle.Spec, ct);
        if (!spec.IsSuccess) return Fail.As<ExcelBundleCheckResultDto>(spec);

        OrderMaterialImportPreviewDto? orderMaterials = null;
        var catalog = await CatalogAsync(bundle.OrderMaterials.Select(r => r.MaterialName), ct);
        if (bundle.HasOrderMaterials)
        {
            if (dto.OrderId is { } orderId)
            {
                var m = await _orderMaterialService.PreviewImportAsync(orderId, bundle.OrderMaterials, ct);
                if (!m.IsSuccess) return Fail.As<ExcelBundleCheckResultDto>(m);
                orderMaterials = MarkNewMaterials(m.Data!, catalog);
            }
            else orderMaterials = await CheckOrderMaterialsWithoutOrderAsync(bundle.OrderMaterials, catalog, ct);
        }

        return Result<ExcelBundleCheckResultDto>.Success(new ExcelBundleCheckResultDto(products, spec.Data!, orderMaterials,
            MissingMaterials(bundle, spec.Data!, orderMaterials)));
    }

    /// <summary>Tên NVL đang dùng trong danh mục (không phân biệt hoa thường).</summary>
    private async Task<HashSet<string>> CatalogAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        var wanted = codes.Where(c => c.Length > 0).Distinct().ToList();
        var rows = await _uow.Repository<Material>().Query().AsNoTracking()
            .Where(m => wanted.Contains(m.MaterialName) && m.Status == MaterialStatuses.Active).Select(m => m.MaterialName).ToListAsync(ct);
        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Có đơn: dòng lỗi chỉ vì tên NVL chưa có trong danh mục thì đổi thành cảnh báo NewMaterial.</summary>
    private static OrderMaterialImportPreviewDto MarkNewMaterials(OrderMaterialImportPreviewDto preview, HashSet<string> catalog)
    {
        var rows = preview.Rows.Select(r => r.Action == ActionError && r.MaterialName is { Length: > 0 } && r.MaterialColor is null
                                            && !catalog.Contains(r.MaterialName)
            ? r with { Action = SpecImportBuilder.ActionNewMaterial, Error = string.Format(Messages.MaterialNotInCatalog, r.MaterialName) }
            : r).ToList();
        var errors = rows.Count(r => r.Action == ActionError);
        return preview with { Rows = rows, ValidCount = rows.Count - errors, ErrorCount = errors - preview.DuplicateCount };
    }

    /// <summary>Gom tên NVL chưa có (định mức + NVL của đơn), gợi ý loại theo chi tiết PANEL và màu áo đang dùng mã đó.</summary>
    private static List<MissingMaterialDto> MissingMaterials(ExcelBundleDto bundle, SpecImportPreviewDto spec,
        OrderMaterialImportPreviewDto? orderMaterials)
    {
        var panels = bundle.Spec.Components.Where(c => c.ComponentType == ComponentTypes.Panel)
            .Select(c => c.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normRows = spec.Norms.Where(n => n.Action == SpecImportBuilder.ActionNewMaterial).ToList();
        var codes = normRows.Select(n => n.MaterialName)
            .Concat(orderMaterials?.Rows.Where(r => r.Action == SpecImportBuilder.ActionNewMaterial).Select(r => r.MaterialName) ?? [])
            .Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return codes.Select(code =>
        {
            var uses = normRows.Where(n => string.Equals(n.MaterialName, code, StringComparison.OrdinalIgnoreCase)).ToList();
            var fabric = uses.Any(n => panels.Contains(n.ComponentName.Trim()));
            // Đơn vị gợi ý lấy từ cột Đơn vị của sheet NVL của đơn (nếu file có).
            var fileRow = bundle.OrderMaterials.FirstOrDefault(r =>
                string.Equals(r.MaterialName.Trim(), code, StringComparison.OrdinalIgnoreCase));
            return new MissingMaterialDto(code, fabric ? MaterialTypes.Fabric : MaterialTypes.Trim,
                uses.Select(n => n.Color.Trim()).Where(c => c.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                fileRow?.Unit);
        }).ToList();
    }

    /// <summary>Chưa có đơn: kiểm định dạng, tên NVL có trong danh mục, số lượng, trùng dòng; phần theo đơn để Pending.</summary>
    private async Task<OrderMaterialImportPreviewDto> CheckOrderMaterialsWithoutOrderAsync(IReadOnlyList<OrderMaterialExcelRow> rows,
        HashSet<string> catalog, CancellationToken ct)
    {
        var codes = rows.Select(r => r.MaterialName).Where(c => c.Length > 0).Distinct().ToList();
        var materials = (await _uow.Repository<Material>().Query().AsNoTracking()
                .Where(m => codes.Contains(m.MaterialName) && m.Status == MaterialStatuses.Active).ToListAsync(ct))
            .GroupBy(m => m.MaterialName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicates = 0;
        var result = new List<OrderMaterialImportRowDto>(rows.Count);
        var validator = new OrderMaterialInputValidator();
        foreach (var row in rows)
        {
            materials.TryGetValue(row.MaterialName, out var material);
            string? duplicate = null;
            if (row.ParseError is null && seen.TryGetValue(row.MaterialName, out var first))
            {
                duplicate = string.Format(Messages.SpecImportDuplicateRow, first);
                duplicates++;
            }
            else if (row.ParseError is null) seen[row.MaterialName] = row.RowNumber;

            // NVL chưa có trong danh mục: kiểm số lượng như NVL có thật (Id tạm 1), dòng hợp lệ thì là cảnh báo NewMaterial.
            var isNew = material is null && row.MaterialName.Length > 0 && !catalog.Contains(row.MaterialName);
            var input = new CreateOrderMaterialLineDto(material?.MaterialId ?? (isNew ? 1 : 0), row.RequiredQty ?? 0, row.CustomerQty ?? 0,
                row.Note);
            var error = row.ParseError ?? duplicate
                        ?? (material is null && !isNew ? string.Format(WarehouseMessages.OrderMaterialImportUnknown, row.MaterialName) : null)
                        ?? validator.Validate(input).Errors.Select(e => e.ErrorMessage).FirstOrDefault();
            var action = error is not null ? ActionError : isNew ? SpecImportBuilder.ActionNewMaterial : SpecImportBuilder.ActionPending;
            var note = error ?? (isNew ? string.Format(Messages.MaterialNotInCatalog, row.MaterialName) : Messages.OrderMaterialPendingOrder);
            result.Add(new OrderMaterialImportRowDto(row.RowNumber, action,
                note, row.MaterialName, material?.Color, material?.Unit,
                row.RequiredQty, row.CustomerQty,
                row.RequiredQty is { } r && row.CustomerQty is { } c ? Math.Max(r - c, 0) : null, row.Note));
        }
        var errors = result.Count(r => r.Action == ActionError);
        return new OrderMaterialImportPreviewDto(result, result.Count - errors, errors - duplicates, duplicates, 0);
    }
}
