using FluentValidation;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-115: nhập Excel định mức mã hàng và bìa của đơn trong một hộp thoại, ở cả màn Mã hàng (Admin) và màn Đơn hàng
/// (Admin, Quản đốc). Xem trước chạy đủ kiểm tra của nút Lưu trên bản đã gộp; Nhập lập lại bản gộp từ file (không tin dữ liệu
/// màn hình gửi) rồi ghi định mức và bìa trong MỘT giao dịch, có lỗi thì không ghi gì. Ghi dùng lại ProductSpecService /
/// OrderMarkerService nên nhật ký và ràng buộc giống hệt khi lưu tay.
/// </summary>
public sealed partial class SpecImportService : ISpecImportService
{
    private readonly IUnitOfWork _uow;
    private readonly IProductSpecService _specs;
    private readonly IOrderMarkerService _markers;
    private readonly IExcelSpecWorkbookParser _parser;
    private readonly IValidator<SaveProductSpecDto> _specValidator;
    private readonly IValidator<SaveOrderMarkersDto> _markerValidator;
    private readonly IClock _clock;

    public SpecImportService(IUnitOfWork uow, IProductSpecService specs, IOrderMarkerService markers, IExcelSpecWorkbookParser parser,
        IValidator<SaveProductSpecDto> specValidator, IValidator<SaveOrderMarkersDto> markerValidator, IClock clock)
    {
        _uow = uow; _specs = specs; _markers = markers; _parser = parser;
        _specValidator = specValidator; _markerValidator = markerValidator; _clock = clock;
    }

    private sealed record Target(string ProductCode, string? OrderCode, OrderMarkerProductDto? OrderProduct);

    private sealed record Plan(SpecImportPreviewDto Preview, SpecImportBuilder Builder);

    public Task<Result<SpecImportPreviewDto>> PreviewAsync(int productId, int? orderId, Stream stream, CancellationToken ct,
        IReadOnlyCollection<string>? parts = null, bool replace = false) =>
        PreviewCoreAsync(productId, orderId, () => _parser.Parse(stream), parts, ct, replace);

    public Task<Result<SpecImportResultDto>> ImportAsync(int productId, int? orderId, Stream stream, CancellationToken ct,
        IReadOnlyCollection<string>? parts = null, bool replace = false) =>
        ImportCoreAsync(productId, orderId, () => _parser.Parse(stream), parts, ct, replace);

    /// <summary>DEV-ALL-122: xem trước từ bảng đã đọc file và sửa trên màn hình (popup kiểm tra Excel).</summary>
    public Task<Result<SpecImportPreviewDto>> PreviewAsync(int productId, int? orderId, SpecWorkbookDto workbook, CancellationToken ct,
        IReadOnlyCollection<string>? parts = null, bool replace = false) =>
        PreviewCoreAsync(productId, orderId, () => workbook, parts, ct, replace);

    /// <summary>DEV-ALL-122: nhập từ bảng đã đọc file và sửa trên màn hình.</summary>
    public Task<Result<SpecImportResultDto>> ImportAsync(int productId, int? orderId, SpecWorkbookDto workbook, CancellationToken ct,
        IReadOnlyCollection<string>? parts = null, bool replace = false) =>
        ImportCoreAsync(productId, orderId, () => workbook, parts, ct, replace);

    private async Task<Result<SpecImportPreviewDto>> PreviewCoreAsync(int productId, int? orderId, Func<SpecWorkbookDto> read,
        IReadOnlyCollection<string>? parts, CancellationToken ct, bool replace = false)
    {
        var plan = await PlanAsync(productId, orderId, read, parts, ct, replace);
        return plan.IsSuccess ? Result<SpecImportPreviewDto>.Success(plan.Data!.Preview) : Fail.As<SpecImportPreviewDto>(plan);
    }

    private async Task<Result<SpecImportResultDto>> ImportCoreAsync(int productId, int? orderId, Func<SpecWorkbookDto> read,
        IReadOnlyCollection<string>? parts, CancellationToken ct, bool replace = false)
    {
        // Tạo màu trong file, lập kế hoạch và ghi trong MỘT giao dịch: lỗi ở bước nào cũng không để lại màu thừa.
        return await _uow.InTransactionAsync(async () =>
        {
            var plan = await PlanAsync(productId, orderId, read, parts, ct, replace, createColors: true);
            if (!plan.IsSuccess) return Fail.As<SpecImportResultDto>(plan);
            var preview = plan.Data!.Preview;
            if (!preview.CanImport)
                return Result<SpecImportResultDto>.Failure(ErrorCodes.SpecImportInvalid,
                    preview.CrossErrors.FirstOrDefault() ?? Messages.SpecImportNothing, ResultKind.Validation);
            return await WriteAsync(productId, orderId, plan.Data.Builder, ct);
        });
    }

    /// <summary>
    /// Màu trong file (định mức NVL, nhóm bìa) mà mã hàng chưa có thì tạo (ProductColor, chưa có size): nhập được định mức ngay khi
    /// tạo mã hàng, không phải chờ đơn hàng. Đơn hàng sau đó dùng lại màu này theo tên.
    /// </summary>
    private async Task EnsureColorsAsync(int productId, SpecWorkbookDto workbook, CancellationToken ct)
    {
        var wanted = workbook.Norms.Select(n => n.Color).Concat(workbook.MarkerGroups.SelectMany(g => g.Colors))
            .Select(c => c.Trim()).Where(c => c.Length > 0).DistinctBy(c => c.ToUpperInvariant()).ToList();
        if (wanted.Count == 0) return;
        var existing = (await _uow.Repository<ProductColor>().Query().AsNoTracking().Where(c => c.ProductId == productId)
            .Select(c => c.Color).ToListAsync(ct)).Select(c => c.Trim().ToUpperInvariant()).ToHashSet();
        var created = wanted.Where(c => !existing.Contains(c.ToUpperInvariant()))
            .Select(c => new ProductColor { ProductId = productId, Color = c, CreatedAt = _clock.Now }).ToList();
        if (created.Count == 0) return;
        await _uow.Repository<ProductColor>().AddRangeAsync(created);
        await _uow.SaveChangesAsync();
    }

    /// <summary>Thêm biến thể tạm (size <see cref="SpecImportBuilder.ColorOnlySize"/>) cho màu đã có mà chưa có size đang dùng.</summary>
    private async Task<ProductSpecDto> WithColorOnlyAsync(int productId, ProductSpecDto spec, CancellationToken ct)
    {
        var known = spec.Variants.Where(v => v.Status == "Active").Select(v => v.Color.Trim().ToUpperInvariant()).ToHashSet();
        var colors = await _uow.Repository<ProductColor>().Query().AsNoTracking().Where(c => c.ProductId == productId)
            .Select(c => c.Color).ToListAsync(ct);
        var extra = colors.Where(c => !known.Contains(c.Trim().ToUpperInvariant()))
            .Select(c => new ProductVariantDto(0, c.Trim(), SpecImportBuilder.ColorOnlySize, "Active")).ToList();
        return extra.Count == 0 ? spec : spec with { Variants = [.. spec.Variants, .. extra] };
    }

    private async Task<Result<SpecImportResultDto>> WriteAsync(int productId, int? orderId, SpecImportBuilder builder, CancellationToken ct)
    {
        if (builder.SpecChanged)
        {
            var spec = await _specs.WriteAsync(productId, builder.Spec, ct);
            if (!spec.IsSuccess) return Fail.As<SpecImportResultDto>(spec);
        }

        var changes = builder.Changes;
        if (orderId is { } id && builder.Markers is not null && changes.OrderMarkers + builder.RemovedMarkers > 0)
        {
            // Định mức có đổi: kiểm đầy đủ lại sau khi ghi (cùng giao dịch) vì nhóm bìa, vải của nhóm là bản mới.
            // Định mức không đổi thì bước xem trước vừa chạy đúng kiểm tra này.
            if (builder.SpecChanged)
            {
                var check = await _markers.CheckAsync(id, productId, builder.Markers, ct);
                if (!check.IsSuccess) return Fail.As<SpecImportResultDto>(check);
            }
            var markers = await _markers.WriteAsync(id, productId, builder.Markers, ct);
            if (!markers.IsSuccess) return Fail.As<SpecImportResultDto>(markers);
        }

        var removed = builder.RemovedTotal;
        return Result<SpecImportResultDto>.Success(changes with { Removed = removed }, string.Format(Messages.SpecImportDone,
            changes.Components, changes.Norms, changes.MarkerGroups, changes.OrderMarkers)
            + (removed > 0 ? string.Format(Messages.SpecImportRemovedDone, removed) : string.Empty));
    }

    private async Task<Result<Plan>> PlanAsync(int productId, int? orderId, Func<SpecWorkbookDto> read,
        IReadOnlyCollection<string>? parts, CancellationToken ct, bool replace = false, bool createColors = false)
    {
        var unknown = parts?.FirstOrDefault(p => !SpecImportParts.All.Contains(p));
        if (unknown is not null)
            return Result<Plan>.Failure(ErrorCodes.SpecImportInvalid, string.Format(Messages.SpecImportPartUnknown, unknown),
                ResultKind.Validation);

        var target = await TargetAsync(productId, orderId, ct);
        if (!target.IsSuccess) return Fail.As<Plan>(target);

        SpecWorkbookDto workbook;
        try
        {
            workbook = read();
        }
        catch (InvalidDataException ex)
        {
            return Result<Plan>.Failure(ErrorCodes.SpecImportInvalid, ex.Message, ResultKind.Validation);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or InvalidOperationException
                                       or NotImplementedException)
        {
            return Result<Plan>.Failure(ErrorCodes.SpecImportInvalid, Messages.SpecImportExcelInvalid, ResultKind.Validation);
        }
        if (parts is { Count: > 0 })
        {
            workbook = SpecImportParts.Keep(workbook, parts);
            if (!workbook.HasComponents && !workbook.HasNorms && !workbook.HasMarkerGroups && !workbook.HasOrderMarkers)
                return Result<Plan>.Failure(ErrorCodes.ImportTableMissing,
                    string.Format(Messages.SpecImportPartMissing, string.Join(", ", parts.Select(SpecImportParts.Label))),
                    ResultKind.Validation);
        }

        if (createColors) await EnsureColorsAsync(productId, workbook, ct);
        var spec = await _specs.GetAsync(productId, ct);
        if (!spec.IsSuccess) return Fail.As<Plan>(spec);
        var specWithColors = await WithColorOnlyAsync(productId, spec.Data!, ct);
        // Xem trước: màu trong file chưa có sẽ được tạo khi Nhập, nên coi như đã có (cùng kết quả với Nhập).
        var (planSpec, colorsToCreate) = AssumeColors(specWithColors, workbook);
        var materials = await MaterialsAsync(workbook.Norms.Select(n => n.MaterialCode), ct);
        var t = target.Data!;
        var usage = replace ? await UsageAsync(productId, spec.Data!, ct) : SpecImportUsage.None;
        var builder = new SpecImportBuilder(planSpec, materials, t.OrderProduct, t.ProductCode, workbook, replace, usage);
        // Kiểm của nút Lưu cần màu có thật: còn màu chưa tạo (chỉ ở xem trước) thì để lúc Nhập kiểm, trong cùng giao dịch.
        var crossErrors = colorsToCreate.Count == 0 ? await CrossErrorsAsync(productId, orderId, builder, ct) : [];
        return Result<Plan>.Success(new Plan(ToPreview(productId, orderId, t, workbook, builder, crossErrors), builder));
    }

    /// <summary>
    /// Chế độ thay thế: dữ liệu cũ đã được dùng ở bảng khác thì không được xóa hay sửa. Định mức đã dùng (lệnh cắt, đơn xin, bàn cắt,
    /// bàn giao, phiếu cấp, tồn BTP) giữ luôn thành phần và cặp màu + chi tiết của nó; nhóm bìa đã có bìa của đơn (bất kỳ đơn nào) giữ nguyên.
    /// </summary>
    private async Task<SpecImportUsage> UsageAsync(int productId, ProductSpecDto spec, CancellationToken ct)
    {
        var usedIds = await NormUsage.UsedIdsAsync(_uow, spec.Norms.Select(n => n.ProductComponentMaterialId).ToList(), ct);
        var usedNorms = spec.Norms.Where(n => usedIds.Contains(n.ProductComponentMaterialId)).ToList();
        var groups = await _uow.Repository<ProductMarker>().Query().AsNoTracking().Where(m => m.ProductId == productId)
            .Select(m => (int)m.MarkerNo).Distinct().ToListAsync(ct);
        return new SpecImportUsage(
            usedNorms.Select(n => n.ComponentName.Trim().ToUpperInvariant()).ToHashSet(),
            usedNorms.Select(n => $"{n.Color.Trim().ToUpperInvariant()}|{n.ComponentName.Trim().ToUpperInvariant()}").ToHashSet(),
            groups.ToHashSet());
    }

    /// <summary>Mã hàng phải có; nếu chọn đơn thì đơn phải có mã hàng này và còn đang chạy (giống điều kiện lưu bìa).</summary>
    private async Task<Result<Target>> TargetAsync(int productId, int? orderId, CancellationToken ct)
    {
        var productCode = await _uow.Repository<Product>().Query().AsNoTracking().Where(p => p.ProductId == productId)
            .Select(p => p.ProductCode).FirstOrDefaultAsync(ct);
        if (productCode is null) return Fail.NotFound<Target>();
        if (orderId is null) return Result<Target>.Success(new Target(productCode, null, null));

        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking().Where(o => o.OrderId == orderId)
            .Select(o => new { o.OrderCode, o.Status }).FirstOrDefaultAsync(ct);
        if (order is null) return Fail.NotFound<Target>();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Result<Target>.Failure(ErrorCodes.OrderMarkerInvalid, Messages.OrderMarkerOrderClosed);

        var markers = await _markers.GetAsync(orderId.Value, ct);
        if (!markers.IsSuccess) return Fail.As<Target>(markers);
        var product = markers.Data!.Products.FirstOrDefault(p => p.ProductId == productId);
        return product is null
            ? Result<Target>.Failure(ErrorCodes.OrderMarkerInvalid, Messages.OrderMarkerProductNotInOrder)
            : Result<Target>.Success(new Target(productCode, order.OrderCode, product));
    }

    private async Task<IReadOnlyDictionary<string, SpecImportMaterial>> MaterialsAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        var wanted = codes.Where(c => c.Length > 0).Distinct().ToList();
        if (wanted.Count == 0) return new Dictionary<string, SpecImportMaterial>();
        var rows = await _uow.Repository<Material>().Query().AsNoTracking()
            .Where(m => wanted.Contains(m.MaterialCode) && m.Status == MaterialStatuses.Active)
            .Select(m => new SpecImportMaterial(m.MaterialId, m.MaterialCode, m.MaterialName, m.Unit, m.Color))
            .ToListAsync(ct);
        return rows.GroupBy(m => m.MaterialCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Kiểm tra của nút Lưu trên bản gộp. Bìa của đơn chỉ kiểm đầy đủ ở đây khi định mức không đổi; định mức có đổi thì nhóm bìa,
    /// vải của nhóm là bản mới chưa ghi, nên kiểm lại khi Nhập (trong cùng giao dịch, sau khi ghi định mức).
    /// </summary>
    private async Task<List<string>> CrossErrorsAsync(int productId, int? orderId, SpecImportBuilder builder, CancellationToken ct)
    {
        var errors = new List<string>();
        if (builder.SpecChanged)
        {
            var format = await _specValidator.ValidateAsync(builder.Spec, ct);
            errors.AddRange(format.Errors.Select(e => e.ErrorMessage).Distinct());
            if (format.IsValid)
            {
                var check = await _specs.CheckAsync(productId, builder.Spec, ct);
                if (!check.IsSuccess) errors.Add(check.Message ?? Messages.SpecImportExcelInvalid);
            }
        }

        if (orderId is { } id && builder.Markers is not null && builder.Changes.OrderMarkers + builder.RemovedMarkers > 0)
        {
            var format = await _markerValidator.ValidateAsync(builder.Markers, ct);
            errors.AddRange(format.Errors.Select(e => e.ErrorMessage).Distinct());
            if (format.IsValid && !builder.SpecChanged)
            {
                var check = await _markers.CheckAsync(id, productId, builder.Markers, ct);
                if (!check.IsSuccess) errors.Add(check.Message ?? Messages.SpecImportExcelInvalid);
            }
        }
        return errors;
    }

    private static SpecImportPreviewDto ToPreview(int productId, int? orderId, Target target, SpecWorkbookDto workbook,
        SpecImportBuilder builder, List<string> crossErrors)
    {
        var actions = builder.AllActions.ToList();
        var errors = actions.Count(a => a == SpecImportBuilder.ActionError);
        var changes = actions.Count(a => a is SpecImportBuilder.ActionNew or SpecImportBuilder.ActionUpdate) + builder.RemovedTotal;
        var notes = builder.Notes.ToList();
        if (builder.SpecChanged && builder.Markers is not null && builder.Changes.OrderMarkers + builder.RemovedMarkers > 0)
            notes.Add(Messages.SpecImportMarkerCheckLater);
        if (changes == 0) notes.Add(Messages.SpecImportNoChange);
        if (builder.KeptUsedNames.Count > 0)
            notes.Add(string.Format(Messages.SpecImportKeptUsed, string.Join("; ", builder.KeptUsedNames)));

        return new SpecImportPreviewDto(productId, target.ProductCode, target.OrderProduct is null ? null : orderId, target.OrderCode,
            builder.Components, builder.Norms, builder.MarkerGroups, builder.OrderMarkers,
            workbook.HasComponents, workbook.HasNorms, workbook.HasMarkerGroups, workbook.HasOrderMarkers && target.OrderProduct is not null,
            actions.Count - errors, errors - builder.DuplicateCount, builder.DuplicateCount, changes,
            crossErrors, notes, changes > 0 && crossErrors.Count == 0, builder.RemovedTotal, builder.RemovedNames,
            target.OrderProduct is null ? [] : SizeOrder.Sort(target.OrderProduct.Sizes));
    }
}
