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

    public SpecImportService(IUnitOfWork uow, IProductSpecService specs, IOrderMarkerService markers, IExcelSpecWorkbookParser parser,
        IValidator<SaveProductSpecDto> specValidator, IValidator<SaveOrderMarkersDto> markerValidator)
    {
        _uow = uow; _specs = specs; _markers = markers; _parser = parser;
        _specValidator = specValidator; _markerValidator = markerValidator;
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
        IReadOnlyCollection<string>? parts = null) => PreviewCoreAsync(productId, orderId, () => workbook, parts, ct);

    /// <summary>DEV-ALL-122: nhập từ bảng đã đọc file và sửa trên màn hình.</summary>
    public Task<Result<SpecImportResultDto>> ImportAsync(int productId, int? orderId, SpecWorkbookDto workbook, CancellationToken ct,
        IReadOnlyCollection<string>? parts = null) => ImportCoreAsync(productId, orderId, () => workbook, parts, ct);

    private async Task<Result<SpecImportPreviewDto>> PreviewCoreAsync(int productId, int? orderId, Func<SpecWorkbookDto> read,
        IReadOnlyCollection<string>? parts, CancellationToken ct, bool replace = false)
    {
        var plan = await PlanAsync(productId, orderId, read, parts, ct, replace);
        return plan.IsSuccess ? Result<SpecImportPreviewDto>.Success(plan.Data!.Preview) : Fail.As<SpecImportPreviewDto>(plan);
    }

    private async Task<Result<SpecImportResultDto>> ImportCoreAsync(int productId, int? orderId, Func<SpecWorkbookDto> read,
        IReadOnlyCollection<string>? parts, CancellationToken ct, bool replace = false)
    {
        var plan = await PlanAsync(productId, orderId, read, parts, ct, replace);
        if (!plan.IsSuccess) return Fail.As<SpecImportResultDto>(plan);
        var preview = plan.Data!.Preview;
        if (!preview.CanImport)
            return Result<SpecImportResultDto>.Failure(ErrorCodes.SpecImportInvalid,
                preview.CrossErrors.FirstOrDefault() ?? Messages.SpecImportNothing, ResultKind.Validation);

        return await _uow.InTransactionAsync(() => WriteAsync(productId, orderId, plan.Data.Builder, ct));
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
        IReadOnlyCollection<string>? parts, CancellationToken ct, bool replace = false)
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

        var spec = await _specs.GetAsync(productId, ct);
        if (!spec.IsSuccess) return Fail.As<Plan>(spec);
        var materials = await MaterialsAsync(workbook.Norms.Select(n => n.MaterialCode), ct);
        var t = target.Data!;
        var builder = new SpecImportBuilder(spec.Data!, materials, t.OrderProduct, t.ProductCode, workbook, replace);
        var crossErrors = await CrossErrorsAsync(productId, orderId, builder, ct);
        return Result<Plan>.Success(new Plan(ToPreview(productId, orderId, t, workbook, builder, crossErrors), builder));
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

        return new SpecImportPreviewDto(productId, target.ProductCode, target.OrderProduct is null ? null : orderId, target.OrderCode,
            builder.Components, builder.Norms, builder.MarkerGroups, builder.OrderMarkers,
            workbook.HasComponents, workbook.HasNorms, workbook.HasMarkerGroups, workbook.HasOrderMarkers && target.OrderProduct is not null,
            actions.Count - errors, errors - builder.DuplicateCount, builder.DuplicateCount, changes,
            crossErrors, notes, changes > 0 && crossErrors.Count == 0, builder.RemovedTotal, builder.RemovedNames);
    }
}
