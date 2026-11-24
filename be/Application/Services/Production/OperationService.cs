using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Application.Validators;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>KI-01 (UC25): công đoạn của mã hàng. Không xóa, chỉ ngừng dùng (sản lượng cũ vẫn trỏ tới).</summary>
public sealed class OperationService : IOperationService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditLogService _audit;
    private readonly IClock _clock;
    private readonly IExcelOperationParser _excel;
    private readonly INotificationService _notifier;

    public OperationService(IUnitOfWork uow, IAuditLogService audit, IClock clock, IExcelOperationParser excel,
        INotificationService notifier)
    {
        _uow = uow; _audit = audit; _clock = clock; _excel = excel; _notifier = notifier;
    }

    private IQueryable<Operation> Operations => _uow.Repository<Operation>().Query();

    public async Task<Result<IReadOnlyList<OperationDto>>> ListAsync(int productId, bool includeDiscontinued, CancellationToken ct)
    {
        if (productId <= 0)
            return Result<IReadOnlyList<OperationDto>>.Failure(ErrorCodes.OperationInvalid, Messages.OperationProductRequired, ResultKind.Validation);

        var productExists = await _uow.Repository<Product>().Query().AsNoTracking()
            .AnyAsync(p => p.ProductId == productId, ct);
        if (!productExists)
            return Fail.As<IReadOnlyList<OperationDto>>(Fail.NotFound());

        var items = await Operations.AsNoTracking()
            .Where(o => o.ProductId == productId && (includeDiscontinued || o.Status == OperationStatuses.Active))
            .OrderBy(o => o.StationSequence).ThenBy(o => o.OperationCode)
            .Select(o => new OperationDto(o.OperationId, o.ProductId, o.Product.ProductCode, o.OperationCode,
                o.OperationName, o.StationSequence, o.SkillLevel, o.StandardTimeSec, o.TeamType, o.IsFinalStep,
                o.Status, null, null, o.OperationGroupId,
                o.OperationGroup == null ? null : o.OperationGroup.GroupName,
                o.OperationComponents.Select(c => c.ProductComponentId).ToList(), null))
            .ToListAsync(ct);

        // Một truy vấn lấy mọi đơn giá của mã hàng, rồi chọn đơn giá hiện hành / đơn giá sắp tới trong bộ nhớ.
        var today = _clock.Today;
        var rates = (await _uow.Repository<PieceRate>().Query().AsNoTracking()
                .Where(r => r.Operation.ProductId == productId)
                .Select(r => new RateRow(r.PieceRateId, r.OperationId, r.Rate, r.EffectiveDate, r.Status, r.DecisionNote))
                .ToListAsync(ct))
            .GroupBy(r => r.OperationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<OperationDto>(items.Count);
        foreach (var item in items)
        {
            rates.TryGetValue(item.OperationId, out var own);
            own ??= [];

            var (current, next) = CurrentAndNextRate(own, today);
            var upcoming = own.Where(r => r.Status == PieceRateStatuses.Approved && r.EffectiveDate > today)
                .OrderBy(r => r.EffectiveDate).FirstOrDefault();

            result.Add(item with
            {
                TeamType = CodeText.ToPascal(item.TeamType), Status = CodeText.ToPascal(item.Status),
                CurrentRate = current, NextRate = next,
                UpcomingRate = upcoming is null
                    ? null
                    : new OperationRateDto(upcoming.PieceRateId, upcoming.Rate, upcoming.EffectiveDate, CodeText.ToPascal(upcoming.Status), null),
            });
        }
        return Result<IReadOnlyList<OperationDto>>.Success(result);
    }

    private sealed record RateRow(
        int PieceRateId, int OperationId, decimal Rate, DateOnly EffectiveDate, string Status, string? DecisionNote);

    /// <summary>
    /// Đơn giá hiện hành (giống FindEffectiveAsync: đã duyệt, ngày hiệu lực lớn nhất không quá hôm nay) và đơn giá
    /// sắp tới / mới nhất khác hiện hành (kèm lý do nếu bị từ chối).
    /// </summary>
    private static (decimal? Current, OperationRateDto? Next) CurrentAndNextRate(List<RateRow> own, DateOnly today)
    {
        var current = own.Where(r => r.Status == PieceRateStatuses.Approved && r.EffectiveDate <= today)
            .OrderByDescending(r => r.EffectiveDate).FirstOrDefault();
        // Bản đang chờ duyệt phải hiện trước, kể cả khi có bản khác ngày muộn hơn, để không cho đặt chồng lên khi Admin chưa quyết định.
        var latest = own.Where(r => r.Status == PieceRateStatuses.PendingApproval).OrderByDescending(r => r.EffectiveDate).FirstOrDefault()
            ?? own.OrderByDescending(r => r.EffectiveDate).ThenByDescending(r => r.PieceRateId).FirstOrDefault();
        var next = latest is not null && latest.PieceRateId != current?.PieceRateId
            ? new OperationRateDto(latest.PieceRateId, latest.Rate, latest.EffectiveDate,
                CodeText.ToPascal(latest.Status), latest.Status == PieceRateStatuses.Rejected ? latest.DecisionNote : null)
            : null;
        return (current?.Rate, next);
    }

    public async Task<Result<int>> CreateAsync(SaveOperationDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var product = await CheckProductAsync(dto.ProductId, ct);
        if (product is not null) return Fail.As<int>(product);

        var fields = ValidateSaveFields(dto);
        if (fields is not null) return Fail.As<int>(fields);

        var code = dto.OperationCode.Trim().ToUpperInvariant();
        var check = await CheckAsync(dto, code, null, ct);
        if (check is not null) return Fail.As<int>(check);

        var operation = new Operation { ProductId = dto.ProductId, Status = OperationStatuses.Active };
        Apply(operation, dto, code);
        var related = await ApplyGroupAndComponentsAsync(operation, dto, ct);
        if (related is not null) return Fail.As<int>(related);

        return await _uow.InTransactionAsync(async () =>   // log cần OperationId nên lưu trước
        {
            List<Operation>? clearedFinals = null;
            try
            {
                if (dto.IsFinalStep) clearedFinals = await MoveFinalStepAsync(dto.ProductId, null, ct);
                await _uow.Repository<Operation>().AddAsync(operation);
                await _uow.SaveChangesAsync();
                _audit.Add(new AuditEntry(nameof(Operation), operation.OperationId, AuditActions.Insert, null, Snapshot(operation)));
                await _uow.SaveChangesAsync();
                return Result<int>.Success(operation.OperationId, Messages.Saved);
            }
            catch
            {
                // Rollback DB đã xử lý bởi InTransactionAsync; khôi phục cờ IsFinalStep trên entity đang theo dõi.
                if (clearedFinals is not null)
                    foreach (var other in clearedFinals) other.IsFinalStep = true;
                throw;
            }
        });
    }

    public async Task<Result> UpdateAsync(int operationId, SaveOperationDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var operation = await _uow.Repository<Operation>().GetByIdAsync(operationId);
        if (operation is null) return Fail.NotFound();

        var fields = ValidateSaveFields(dto);
        if (fields is not null) return fields;

        var code = dto.OperationCode.Trim().ToUpperInvariant();
        // Mã hàng của công đoạn không đổi: sản lượng và đơn giá cũ gắn với mã hàng đó.
        var check = await CheckAsync(dto with { ProductId = operation.ProductId }, code, operation, ct);
        if (check is not null) return check;

        return await _uow.InTransactionAsync(async () =>
        {
            if (dto.IsFinalStep && operation.Status == OperationStatuses.Active)
                await MoveFinalStepAsync(operation.ProductId, operationId, ct);
            var before = Snapshot(operation);
            Apply(operation, dto, code);
            var related = await ApplyGroupAndComponentsAsync(operation, dto, ct);
            if (related is not null) return related;
            _audit.Add(new AuditEntry(nameof(Operation), operationId, AuditActions.Update, before, Snapshot(operation)));
            await _uow.SaveChangesAsync();
            return Result.Success(Messages.Saved);
        });
    }

    public async Task<Result> DiscontinueAsync(int operationId, CancellationToken ct)
    {
        var operation = await _uow.Repository<Operation>().GetByIdAsync(operationId);
        if (operation is null) return Fail.NotFound();
        if (operation.Status == OperationStatuses.Discontinued) return Result.Success(Messages.Saved);
        // Màn giao việc chỉ hiện công đoạn đang dùng và sản lượng chặn công đoạn đã ngừng: việc đang giao sẽ không thu hồi, không báo xong được.
        if (await HasOpenWorkAsync(operationId, ct))
            return Result.Failure(ErrorCodes.OperationHasOpenWork, Messages.OperationHasOpenWork);
        // Mã hàng phải luôn còn đúng một công đoạn may cuối đang dùng.
        if (operation.IsFinalStep
            && !await FinalStepExistsAsync(operation.ProductId, operationId, ct))
            return Result.Failure(ErrorCodes.OperationFinalStepRequired, Messages.OperationFinalStepRequired);

        operation.Status = OperationStatuses.Discontinued;
        _audit.Add(new AuditEntry(nameof(Operation), operationId, AuditActions.Update,
            new { Status = OperationStatuses.Active }, new { operation.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    /// <summary>
    /// Chế độ thay thế: xóa công đoạn chưa từng được dùng cùng đơn giá và liên kết chi tiết của nó (công đoạn đã dùng đã bị loại khỏi
    /// danh sách xóa lúc lập kế hoạch nên không bao giờ bị sửa).
    /// </summary>
    private async Task RemoveUnusedOperationsAsync(IReadOnlyList<Operation> removals, CancellationToken ct)
    {
        if (removals.Count == 0) return;
        var ids = removals.Select(o => o.OperationId).ToList();
        _uow.Repository<PieceRate>().RemoveRange(await _uow.Repository<PieceRate>().Query().Where(r => ids.Contains(r.OperationId)).ToListAsync(ct));
        _uow.Repository<OperationComponent>().RemoveRange(
            await _uow.Repository<OperationComponent>().Query().Where(l => ids.Contains(l.OperationId)).ToListAsync(ct));
        foreach (var operation in removals)
        {
            _audit.Add(new AuditEntry(nameof(Operation), operation.OperationId, AuditActions.Delete, Snapshot(operation), null,
                Messages.OperationImportRemoveReason));
            _uow.Repository<Operation>().Remove(operation);
        }
    }

    /// <summary>Công đoạn đã có phiếu giao việc, phân công, sản lượng hoặc phiếu hàng hỏng (kể cả đã xong): không xóa, không sửa khi thay thế.</summary>
    private async Task<bool> IsUsedAsync(int operationId, CancellationToken ct) =>
        await _uow.Repository<WorkAllocation>().Query().AnyAsync(x => x.OperationId == operationId, ct)
        || await _uow.Repository<OperationAssignment>().Query().AnyAsync(x => x.OperationId == operationId, ct)
        || await _uow.Repository<OutputRecord>().Query().AnyAsync(x => x.OperationId == operationId, ct)
        || await _uow.Repository<DefectRecord>().Query().AnyAsync(x => x.OperationId == operationId, ct);

    private Task<bool> HasOpenWorkAsync(int operationId, CancellationToken ct)
    {
        var openLines = _uow.Repository<OrderLine>().Query().Where(l => !OrderStatuses.Closed.Contains(l.Order.Status)).Select(l => l.OrderLineId);
        var pending = _uow.Repository<WorkAllocation>().Query().Where(w => w.OperationId == operationId
            && w.Status == HandoverStatuses.Pending && openLines.Contains(w.OrderLineId)).Select(w => w.OperationId);
        var held = _uow.Repository<VwWorkerOutputCap>().Query().Where(c => c.OperationId == operationId
            && c.RemainingQty > 0 && openLines.Contains(c.OrderLineId)).Select(c => c.OperationId);
        return pending.Concat(held).AnyAsync(ct);
    }

    /// <summary>Mã hàng phải còn và đang sản xuất mới thêm công đoạn được.</summary>
    private async Task<Result?> CheckProductAsync(int productId, CancellationToken ct)
    {
        var product = await _uow.Repository<Product>().Query().AsNoTracking()
            .Where(p => p.ProductId == productId).Select(p => new { p.ProductCode, p.Status }).FirstOrDefaultAsync(ct);
        if (product is null) return Fail.NotFound();
        return product.Status == RecordStatus.Active
            ? null
            : Result.Failure(ErrorCodes.ProductNotActive, string.Format(Messages.ProductNotActive, product.ProductCode));
    }

    public async Task<Result> ReactivateAsync(int operationId, CancellationToken ct)
    {
        var operation = await _uow.Repository<Operation>().GetByIdAsync(operationId);
        if (operation is null) return Fail.NotFound();
        if (operation.Status == OperationStatuses.Active) return Result.Success(Messages.Saved);

        // Dùng lại công đoạn cuối đã ngừng thì không được có công đoạn cuối khác đang dùng.
        if (operation.IsFinalStep && await FinalStepExistsAsync(operation.ProductId, operationId, ct))
            return Result.Failure(ErrorCodes.OperationFinalStepExists, Messages.OperationFinalStepExists);

        operation.Status = OperationStatuses.Active;
        _audit.Add(new AuditEntry(nameof(Operation), operationId, AuditActions.Update,
            new { Status = OperationStatuses.Discontinued }, new { operation.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    // ---------- Nhập công đoạn (kèm đơn giá) từ file Excel quy trình công nghệ ----------

    /// <param name="ComponentIds">DEV-ALL-116: chi tiết PANEL công đoạn làm trên (null = file không có cột CHI TIẾT, giữ nguyên).</param>
    private sealed record PlannedRow(
        OperationImportRowDto Preview, OperationExcelRowDto Raw, Operation? Existing, PieceRate? ExistingRate,
        IReadOnlyList<int>? ComponentIds = null);

    /// <param name="Removals">Chế độ thay thế: công đoạn may không có trong file và chưa từng dùng nên xóa được.</param>
    /// <param name="Blocked">Công đoạn không có trong file nhưng đã được dùng nên giữ nguyên.</param>
    private sealed record ImportPlan(
        OperationExcelSheetDto Sheet, IReadOnlyList<string> SheetNames, bool ProductHasFinalStep, IReadOnlyList<PlannedRow> Rows,
        ImportRefs? Refs, IReadOnlyList<Operation> Removals, IReadOnlyList<string> Blocked);

    /// <summary>
    /// DEV-ALL-116: dữ liệu để nhập cột NHÓM CÔNG ĐOẠN / CHI TIẾT. Chỉ nạp khi file có một trong hai cột, nên file quy trình
    /// công nghệ cũ nhập y như trước.
    /// </summary>
    private sealed record ImportRefs(
        Dictionary<string, ProductComponent> Components, Dictionary<string, OperationGroup> Groups,
        Dictionary<int, string> GroupNameById, List<OperationComponent> Links);

    private const string ActionNew = "New";
    private const string ActionUpdate = "Update";
    private const string ActionUnchanged = "Unchanged";
    private const string ActionError = "Error";
    private const string RateNone = "None";
    private const string RateSkipped = "Skipped";

    /// <summary>
    /// DEV-ALL-179: kiểm từng ô của dòng như bộ đọc file (dòng sửa trên màn hình không đi qua bộ đọc): tên, bậc, TGCT, TIỀN, nhóm.
    /// Dòng đọc từ file đã qua các kiểm tra này nên không bị báo lại.
    /// </summary>
    private static string? EditedRowProblem(OperationExcelRowDto raw, OperationExcelSheetDto sheet)
    {
        if (string.IsNullOrWhiteSpace(raw.Name)) return Messages.Required("tên công đoạn");
        if (raw.Name.Trim().Length > 200) return "Tên công đoạn dài quá 200 ký tự.";
        if (raw.SkillLevel is { } skill && (skill < OperationLimits.MinSkillLevel || skill > OperationLimits.MaxSkillLevel))
            return "Bậc phải là số nguyên từ 1 đến 9.";
        if (raw.StandardTimeSec is { } time)
        {
            if (time < 0) return "TGCT không được âm.";
            if (decimal.Round(time, 2) != time) return "TGCT tối đa 2 số lẻ.";
            if (time > OperationLimits.MaxStandardTimeSec) return $"TGCT không được vượt quá {OperationLimits.MaxStandardTimeSec}.";
        }
        if (raw.Rate is { } rate)
        {
            if (rate < 0) return "TIỀN không được âm.";
            if (decimal.Round(rate, 2) != rate) return "TIỀN tối đa 2 số lẻ.";
            if (rate > PieceRateLimits.MaxRate) return string.Format(Messages.PieceRateTooHigh, PieceRateLimits.MaxRate);
        }
        if (sheet.HasGroupColumn && raw.GroupName is { Length: > OperationLimits.MaxGroupNameLength })
            return $"Nhóm công đoạn dài quá {OperationLimits.MaxGroupNameLength} ký tự.";
        return null;
    }

    /// <summary>Mã công đoạn sinh từ số thứ tự trong file: STT 7 → "OP07". Nhập lại cùng file thì khớp đúng công đoạn cũ.</summary>
    private static string CodeFor(int stt) => $"OP{stt:00}";

    public async Task<Result<OperationImportPreviewDto>> PreviewImportAsync(
        int productId, Stream stream, string? sheetName, DateOnly? effectiveDate, CancellationToken ct, bool replace = false) =>
        ToPreview(await BuildImportPlanAsync(productId, stream, sheetName, effectiveDate, ct, replace));

    /// <summary>DEV-ALL-179: xem trước từ bảng đã đọc và sửa trên màn hình (không đọc lại file), không ghi gì.</summary>
    public async Task<Result<OperationImportPreviewDto>> PreviewImportAsync(
        int productId, OperationExcelSheetDto sheet, DateOnly? effectiveDate, CancellationToken ct, bool replace = false) =>
        ToPreview(await BuildImportPlanAsync(productId, sheet, effectiveDate, ct, replace));

    private static Result<OperationImportPreviewDto> ToPreview(Result<ImportPlan> plan)
    {
        if (!plan.IsSuccess) return Fail.As<OperationImportPreviewDto>(plan);

        var p = plan.Data!;
        int Count(string action) => p.Rows.Count(r => r.Preview.Action == action);
        int RateCount(string action) => p.Rows.Count(r => r.Preview.RateAction == action);
        var finalInFile = p.Rows.Where(r => r.Raw.IsFinal && r.Preview.Action != ActionError).Select(r => (int?)r.Preview.Stt)
            .FirstOrDefault();
        return Result<OperationImportPreviewDto>.Success(new OperationImportPreviewDto(
            p.Sheet.SheetName, p.SheetNames, p.Sheet.ProductText, Count(ActionNew), Count(ActionUpdate),
            Count(ActionUnchanged), Count(ActionError), p.ProductHasFinalStep, p.Sheet.HasRateColumn,
            RateCount(ActionNew), RateCount(ActionUpdate), RateCount(ActionUnchanged), RateCount(RateSkipped),
            p.Rows.Select(r => r.Preview).ToList(), p.Sheet.HasGroupColumn, p.Sheet.HasComponentColumn, finalInFile,
            p.Removals.Count, p.Removals.Select(o => $"{o.OperationCode} {o.OperationName}").ToList(), p.Blocked,
            p.Rows.Count(r => r.Preview.Warning is not null)));
    }

    public Task<Result<OperationImportResultDto>> ImportAsync(
        int productId, Stream stream, string? sheetName, int? finalStt, DateOnly? effectiveDate, bool submitRates,
        CancellationToken ct, bool replace = false) =>
        ImportCoreAsync(productId, finalStt, effectiveDate, submitRates, ct,
            rateEffective => BuildImportPlanAsync(productId, stream, sheetName, rateEffective, ct, replace));

    /// <summary>DEV-ALL-179: nhập từ bảng đã đọc và sửa trên màn hình.</summary>
    public Task<Result<OperationImportResultDto>> ImportAsync(
        int productId, OperationExcelSheetDto sheet, int? finalStt, DateOnly? effectiveDate, bool submitRates,
        CancellationToken ct, bool replace = false) =>
        ImportCoreAsync(productId, finalStt, effectiveDate, submitRates, ct,
            rateEffective => BuildImportPlanAsync(productId, sheet, rateEffective, ct, replace));

    private async Task<Result<OperationImportResultDto>> ImportCoreAsync(
        int productId, int? finalStt, DateOnly? effectiveDate, bool submitRates, CancellationToken ct,
        Func<DateOnly?, Task<Result<ImportPlan>>> buildPlan)
    {
        // submitRates mà không chọn ngày → dùng hôm nay (đơn giá chờ duyệt phải có EffectiveDate).
        var rateEffective = effectiveDate ?? (submitRates ? _clock.Today : (DateOnly?)null);
        var plan = await buildPlan(rateEffective);
        if (!plan.IsSuccess) return Fail.As<OperationImportResultDto>(plan);

        if (plan.Data!.Rows.Count(r => r.Raw.IsFinal) > 1)
            return Result<OperationImportResultDto>.Failure(ErrorCodes.OperationFinalStepExists,
                Messages.OperationFinalStepExists);

        var valid = plan.Data!.Rows.Where(r => r.Preview.Action != ActionError).ToList();
        if (valid.Count == 0)
            return Result<OperationImportResultDto>.Failure(ErrorCodes.OperationExcelEmpty, Messages.OperationExcelEmpty,
                ResultKind.Validation);
        var skipped = plan.Data.Rows.Count - valid.Count;

        var finalError = await CheckFinalStepAsync(productId, valid, finalStt, ct, plan.Data.Removals.Select(o => o.OperationId).ToList());
        if (finalError is not null) return finalError;

        return await _uow.InTransactionAsync(
            () => ApplyImportAsync(productId, plan.Data!, valid, finalStt, rateEffective, submitRates, skipped, ct));
    }

    private sealed record RowChanges(
        List<Operation> Created, List<(Operation Operation, object Before)> Updated,
        Dictionary<int, Operation> OperationByStt, int Unchanged);

    /// <summary>Áp các dòng hợp lệ lên công đoạn: dòng mới tạo thực thể, dòng đã có cập nhật nếu thay đổi (hoặc được chọn làm bước cuối).</summary>
    private RowChanges ApplyRows(int productId, IReadOnlyList<PlannedRow> valid, int? finalStt, ImportPlan plan)
    {
        var changes = ApplyBaseRows(productId, valid, finalStt);
        if (plan.Refs is null) return changes;
        foreach (var row in valid)
        {
            var operation = changes.OperationByStt[row.Preview.Stt];
            var touched = changes.Created.Contains(operation) || changes.Updated.Any(u => u.Operation == operation);
            if (touched) ApplyGroupAndComponents(operation, row, plan.Sheet, plan.Refs, productId);
        }
        return changes;
    }

    /// <summary>
    /// DEV-ALL-116: nhóm công đoạn theo tên (chưa có thì tạo, dùng chung trong một lần nhập) và bộ chi tiết của công đoạn
    /// (thay bằng bộ trong file). Cột không có trong file thì giữ nguyên.
    /// </summary>
    private void ApplyGroupAndComponents(Operation operation, PlannedRow row, OperationExcelSheetDto sheet, ImportRefs refs, int productId)
    {
        if (sheet.HasGroupColumn)
        {
            var name = row.Raw.GroupName;
            if (name is null)
            {
                operation.OperationGroup = null;
                operation.OperationGroupId = null;
            }
            else
            {
                if (!refs.Groups.TryGetValue(name, out var group))
                {
                    group = new OperationGroup { ProductId = productId, GroupName = name };
                    refs.Groups[name] = group;
                }
                operation.OperationGroup = group;
            }
        }

        if (row.ComponentIds is null) return;
        var current = refs.Links.Where(l => operation.OperationId != 0 && l.OperationId == operation.OperationId).ToList();
        _uow.Repository<OperationComponent>().RemoveRange(current.Where(l => !row.ComponentIds.Contains(l.ProductComponentId)).ToList());
        foreach (var id in row.ComponentIds.Where(id => current.All(l => l.ProductComponentId != id)))
            operation.OperationComponents.Add(new OperationComponent { ProductComponentId = id, ProductId = productId });
    }

    private RowChanges ApplyBaseRows(int productId, IReadOnlyList<PlannedRow> valid, int? finalStt)
    {
        var created = new List<Operation>();
        var updated = new List<(Operation Operation, object Before)>();
        var operationByStt = new Dictionary<int, Operation>();
        var unchanged = 0;

        foreach (var row in valid)
        {
            var p = row.Preview;
            var markFinal = finalStt == p.Stt;
            if (row.Existing is null)
            {
                var operation = new Operation
                {
                    ProductId = productId, Status = OperationStatuses.Active, TeamType = TeamTypes.Sewing,
                };
                ApplyImported(operation, p);
                operation.IsFinalStep = markFinal;
                created.Add(operation);
                operationByStt[p.Stt] = operation;
                continue;
            }

            var existing = row.Existing;
            operationByStt[p.Stt] = existing;
            if (p.Action == ActionUnchanged && !(markFinal && !existing.IsFinalStep))
            {
                unchanged++;
                continue;
            }

            var before = Snapshot(existing);
            ApplyImported(existing, p);
            if (markFinal) existing.IsFinalStep = true;
            updated.Add((existing, before));
        }
        return new RowChanges(created, updated, operationByStt, unchanged);
    }

    private async Task<Result<OperationImportResultDto>> ApplyImportAsync(int productId, ImportPlan plan,
        IReadOnlyList<PlannedRow> valid, int? finalStt, DateOnly? effectiveDate, bool submitRates, int skipped,
        CancellationToken ct)
    {
        var (created, updated, operationByStt, unchanged) = ApplyRows(productId, valid, finalStt, plan);
        var newGroups = plan.Refs?.Groups.Values.Where(g => g.OperationGroupId == 0).ToList() ?? [];
        if (newGroups.Count > 0) await _uow.Repository<OperationGroup>().AddRangeAsync(newGroups);
        if (created.Count > 0) await _uow.Repository<Operation>().AddRangeAsync(created);
        await _uow.SaveChangesAsync();   // lấy OperationId của dòng mới để ghi nhật ký và gắn đơn giá

        foreach (var operation in created)
            _audit.Add(new AuditEntry(nameof(Operation), operation.OperationId, AuditActions.Insert, null, Snapshot(operation)));
        foreach (var (operation, before) in updated)
            _audit.Add(new AuditEntry(nameof(Operation), operation.OperationId, AuditActions.Update, before, Snapshot(operation)));

        await RemoveUnusedOperationsAsync(plan.Removals, ct);

        var rateStats = await ApplyImportedRatesAsync(valid, operationByStt, effectiveDate, submitRates);
        await _uow.SaveChangesAsync();

        if (rateStats.Submitted > 0)
            await _notifier.NotifyRoleAsync(RoleCodes.Admin, new NotificationMessage(NotificationSources.Production,
                Messages.NotifyPieceRateSubmittedTitle,
                string.Format(Messages.NotifyPieceRatesImportedBody, plan.Sheet.SheetName, rateStats.Submitted),
                null, null, null), ct);

        var result = new OperationImportResultDto(created.Count, updated.Count, unchanged, skipped,
            rateStats.Created, rateStats.Updated, rateStats.Unchanged, rateStats.Skipped, plan.Removals.Count);
        return Result<OperationImportResultDto>.Success(result, string.Format(Messages.OperationImportDone,
            created.Count, updated.Count, unchanged, skipped, rateStats.Created, rateStats.Updated,
            rateStats.Unchanged, rateStats.Skipped)
            + (plan.Removals.Count > 0 ? string.Format(Messages.OperationImportRemoved, plan.Removals.Count) : string.Empty));
    }

    /// <summary>Khi chọn dòng làm công đoạn cuối: dòng phải hợp lệ và mã hàng chưa có công đoạn cuối khác. Trả về lỗi hoặc null.</summary>
    private async Task<Result<OperationImportResultDto>?> CheckFinalStepAsync(int productId, IReadOnlyList<PlannedRow> valid,
        int? finalStt, CancellationToken ct, IReadOnlyCollection<int>? removedIds = null)
    {
        if (finalStt is null) return null;
        var target = valid.FirstOrDefault(r => r.Preview.Stt == finalStt);
        if (target is null)
            return Result<OperationImportResultDto>.Failure(ErrorCodes.OperationImportFinalNotFound,
                Messages.OperationImportFinalNotFound, ResultKind.Validation);
        if (target.Existing is { Status: not OperationStatuses.Active })
            return Result<OperationImportResultDto>.Failure(ErrorCodes.OperationNotActive, Messages.OperationNotActive);
        if (await FinalStepExistsAsync(productId, target.Existing?.OperationId, ct, removedIds))
            return Result<OperationImportResultDto>.Failure(ErrorCodes.OperationFinalStepExists,
                Messages.OperationFinalStepExists);
        return null;
    }

    private sealed record RateStats(int Created, int Updated, int Unchanged, int Skipped, int Submitted);

    /// <summary>Tạo / cập nhật đơn giá từ cột TIỀN theo kế hoạch đã tính; Nháp, hoặc Chờ duyệt nếu gửi duyệt luôn.</summary>
    private async Task<RateStats> ApplyImportedRatesAsync(
        IReadOnlyList<PlannedRow> rows, IReadOnlyDictionary<int, Operation> operationByStt, DateOnly? effectiveDate,
        bool submit)
    {
        var unchanged = rows.Count(r => r.Preview.RateAction == ActionUnchanged);
        var skipped = rows.Count(r => r.Preview.RateAction == RateSkipped);
        if (effectiveDate is null) return new RateStats(0, 0, unchanged, skipped, 0);

        var (created, updated) = ApplyRateRows(rows, operationByStt, effectiveDate.Value, submit);

        if (created.Count > 0) await _uow.Repository<PieceRate>().AddRangeAsync(created);
        await _uow.SaveChangesAsync();   // lấy PieceRateId để ghi nhật ký

        foreach (var rate in created)
            _audit.Add(new AuditEntry(nameof(PieceRate), rate.PieceRateId, AuditActions.Insert, null, RateSnapshot(rate)));
        foreach (var (rate, before) in updated)
            _audit.Add(new AuditEntry(nameof(PieceRate), rate.PieceRateId, AuditActions.Update, before, RateSnapshot(rate)));

        return new RateStats(created.Count, updated.Count, unchanged, skipped, submit ? created.Count + updated.Count : 0);
    }

    /// <summary>Dựng đơn giá mới và cập nhật đơn giá cũ (Nháp hoặc Chờ duyệt nếu gửi duyệt luôn) theo kế hoạch từng dòng.</summary>
    private (List<PieceRate> Created, List<(PieceRate Rate, object Before)> Updated) ApplyRateRows(
        IReadOnlyList<PlannedRow> rows, IReadOnlyDictionary<int, Operation> operationByStt, DateOnly effectiveDate,
        bool submit)
    {
        var now = _clock.Now;
        var status = submit ? PieceRateStatuses.PendingApproval : PieceRateStatuses.Draft;
        var created = new List<PieceRate>();
        var updated = new List<(PieceRate Rate, object Before)>();

        foreach (var row in rows)
        {
            if (row.Preview.RateAction is not (ActionNew or ActionUpdate) || row.Raw.Rate is null) continue;
            var operation = operationByStt[row.Preview.Stt];

            if (row.Preview.RateAction == ActionNew)
            {
                created.Add(new PieceRate
                {
                    OperationId = operation.OperationId, Rate = row.Raw.Rate.Value, EffectiveDate = effectiveDate,
                    Status = status, SubmittedAt = submit ? now : null,
                });
                continue;
            }

            var rate = row.ExistingRate!;
            var before = RateSnapshot(rate);
            rate.Rate = row.Raw.Rate.Value;
            rate.Status = status;
            rate.DecisionNote = null;
            rate.SubmittedAt = submit ? now : null;
            updated.Add((rate, before));
        }
        return (created, updated);
    }

    private async Task<Result<ImportPlan>> BuildImportPlanAsync(
        int productId, Stream stream, string? sheetName, DateOnly? effectiveDate, CancellationToken ct, bool replace = false)
    {
        var product = await CheckProductAsync(productId, ct);
        if (product is not null) return Fail.As<ImportPlan>(product);

        var parsed = ParseSheets(stream);
        if (!parsed.IsSuccess) return Fail.As<ImportPlan>(parsed);
        var sheets = parsed.Data!;

        if (sheets.Count == 0)
            return Result<ImportPlan>.Failure(ErrorCodes.OperationExcelInvalid, Messages.OperationExcelInvalid,
                ResultKind.Validation);

        var sheet = string.IsNullOrWhiteSpace(sheetName)
            ? sheets[0]
            : sheets.FirstOrDefault(s => string.Equals(s.SheetName, sheetName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (sheet is null)
            return Result<ImportPlan>.Failure(ErrorCodes.OperationImportSheetNotFound, Messages.OperationImportSheetNotFound,
                ResultKind.Validation);

        return await BuildImportPlanAsync(productId, sheet, sheets.Select(s => s.SheetName).ToList(), effectiveDate, ct, replace);
    }

    /// <summary>DEV-ALL-179: lập kế hoạch từ một sheet đã đọc và sửa trên màn hình (chỉ kiểm mã hàng, không đọc file).</summary>
    private async Task<Result<ImportPlan>> BuildImportPlanAsync(
        int productId, OperationExcelSheetDto sheet, DateOnly? effectiveDate, CancellationToken ct, bool replace = false)
    {
        var product = await CheckProductAsync(productId, ct);
        if (product is not null) return Fail.As<ImportPlan>(product);
        return await BuildImportPlanAsync(productId, sheet, [sheet.SheetName], effectiveDate, ct, replace);
    }

    private async Task<Result<ImportPlan>> BuildImportPlanAsync(
        int productId, OperationExcelSheetDto sheet, IReadOnlyList<string> sheetNames, DateOnly? effectiveDate, CancellationToken ct,
        bool replace)
    {
        var existingOps = await Operations.Where(o => o.ProductId == productId).ToListAsync(ct);
        var byCode = existingOps.ToDictionary(o => o.OperationCode, StringComparer.OrdinalIgnoreCase);
        var ratesByOperation = (await _uow.Repository<PieceRate>().Query().Where(r => r.Operation.ProductId == productId)
                .ToListAsync(ct))
            .GroupBy(r => r.OperationId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var refs = sheet.HasGroupColumn || sheet.HasComponentColumn ? await LoadImportRefsAsync(productId, ct) : null;
        var rows = PlanRows(sheet, byCode, ratesByOperation, effectiveDate, refs);

        var hasFinal = existingOps.Any(o => o.IsFinalStep && o.Status == OperationStatuses.Active);
        var removals = new List<Operation>();
        var blocked = new List<string>();
        // Thay thế: công đoạn may mà mã (OP + STT) không có trong file thì xóa nếu chưa từng dùng, đã dùng thì giữ nguyên (báo lại);
        // file không có dòng đọc được thì không đụng gì
        if (replace && rows.Any(r => r.Preview.Action != ActionError))
        {
            var fileCodes = rows.Select(r => r.Preview.OperationCode).Where(c => !string.IsNullOrEmpty(c)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var o in existingOps.Where(o => o.Status == OperationStatuses.Active && o.TeamType == TeamTypes.Sewing && !fileCodes.Contains(o.OperationCode)))
            {
                if (await IsUsedAsync(o.OperationId, ct)) blocked.Add($"{o.OperationCode} {o.OperationName}");
                else removals.Add(o);
            }
        }
        return Result<ImportPlan>.Success(new ImportPlan(sheet, sheetNames, hasFinal, rows, refs, removals, blocked));
    }

    /// <summary>Thành phần, nhóm công đoạn và liên kết công đoạn ↔ chi tiết của mã hàng (DEV-ALL-116), có theo dõi để sửa được.</summary>
    private async Task<ImportRefs> LoadImportRefsAsync(int productId, CancellationToken ct)
    {
        var components = await _uow.Repository<ProductComponent>().Query()
            .Where(c => c.ProductId == productId && c.Status == RecordStatus.Active).ToListAsync(ct);
        var groups = await _uow.Repository<OperationGroup>().Query().Where(g => g.ProductId == productId).ToListAsync(ct);
        var links = await _uow.Repository<OperationComponent>().Query().Where(l => l.ProductId == productId).ToListAsync(ct);
        return new ImportRefs(
            components.GroupBy(c => c.ComponentName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase),
            groups.GroupBy(g => g.GroupName.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase),
            groups.ToDictionary(g => g.OperationGroupId, g => g.GroupName),
            links);
    }

    /// <summary>Đọc các sheet từ file Excel; file hỏng thì trả lỗi validation.</summary>
    private Result<IReadOnlyList<OperationExcelSheetDto>> ParseSheets(Stream stream)
    {
        try
        {
            return Result<IReadOnlyList<OperationExcelSheetDto>>.Success(_excel.Parse(stream));
        }
        catch (InvalidDataException ex)
        {
            return Result<IReadOnlyList<OperationExcelSheetDto>>.Failure(ErrorCodes.OperationExcelInvalid, ex.Message, ResultKind.Validation);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException
                                       or InvalidOperationException or NotImplementedException)
        {
            return Result<IReadOnlyList<OperationExcelSheetDto>>.Failure(ErrorCodes.OperationExcelInvalid, Messages.OperationExcelInvalid,
                ResultKind.Validation);
        }
    }

    /// <summary>Lập kế hoạch cho từng dòng của sheet: lỗi / thêm mới / cập nhật / không đổi, kèm cách xử lý đơn giá.</summary>
    private static List<PlannedRow> PlanRows(OperationExcelSheetDto sheet, IReadOnlyDictionary<string, Operation> byCode,
        IReadOnlyDictionary<int, List<PieceRate>> ratesByOperation, DateOnly? effectiveDate, ImportRefs? refs)
    {
        var duplicateStt = sheet.Rows.GroupBy(r => r.Stt).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var manyFinals = sheet.Rows.Count(r => r.IsFinal) > 1;
        var rows = new List<PlannedRow>(sheet.Rows.Count);
        foreach (var raw in sheet.Rows)
        {
            var code = CodeFor(raw.Stt);
            var error = raw.ParseError;
            if (error is null && (raw.Stt < 1 || raw.Stt > short.MaxValue))
                error = Messages.OperationImportSttOutOfRange;
            if (error is null && duplicateStt.Contains(raw.Stt)) error = $"Trùng STT {raw.Stt} trong file.";
            if (error is null && manyFinals && raw.IsFinal) error = Messages.OperationImportManyFinals;
            error ??= EditedRowProblem(raw, sheet);
            var (componentIds, componentNames, componentWarning) = ResolveComponents(raw, sheet, refs);
            var group = sheet.HasGroupColumn ? raw.GroupName : null;

            if (error is not null)
            {
                rows.Add(new PlannedRow(new OperationImportRowDto(raw.RowNumber, raw.Stt, code, raw.Name, raw.SkillLevel,
                    raw.StandardTimeSec, raw.Equipment, raw.Rate, ActionError, error, GroupName: group,
                    ComponentNames: componentNames, Warning: componentWarning), raw, null, null));
                continue;
            }

            byCode.TryGetValue(code, out var existing);
            var note = existing is null || refs is null ? null : ChangeNote(existing, raw, sheet, refs, componentIds);
            var action = existing is null ? ActionNew : IsSame(existing, raw) && note is null ? ActionUnchanged : ActionUpdate;
            var (rateAction, rateNote, existingRate) = PlanRate(raw, existing, effectiveDate, ratesByOperation);
            rows.Add(new PlannedRow(new OperationImportRowDto(raw.RowNumber, raw.Stt, code, raw.Name, raw.SkillLevel,
                raw.StandardTimeSec, raw.Equipment, raw.Rate, action, null, rateAction, rateNote, group, componentNames, note, componentWarning),
                raw, existing, existingRate, componentIds));
        }
        return rows;
    }

    /// <summary>
    /// DEV-ALL-116: tên trong cột CHI TIẾT → chi tiết PANEL của mã hàng. Tên không có ở mã hàng hoặc là phụ liệu không chặn dòng: bỏ qua
    /// tên đó và trả cảnh báo (xem trước hiện chi tiết). Không có tên nào gắn được thì giữ nguyên bộ chi tiết đang có (ids = null).
    /// Trả (null, null, null) khi file không có cột.
    /// </summary>
    private static (IReadOnlyList<int>? Ids, IReadOnlyList<string>? Names, string? Warning) ResolveComponents(
        OperationExcelRowDto raw, OperationExcelSheetDto sheet, ImportRefs? refs)
    {
        if (!sheet.HasComponentColumn || refs is null || raw.ComponentNames is null) return (null, null, null);
        var ids = new List<int>();
        var names = new List<string>();
        var skipped = new List<string>();
        foreach (var name in raw.ComponentNames)
        {
            if (!refs.Components.TryGetValue(name, out var component))
                skipped.Add(string.Format(Messages.OperationImportComponentUnknown, name));
            else if (component.ComponentType != ComponentTypes.Panel)
                skipped.Add(string.Format(Messages.OperationImportComponentNotPanel, name));
            else if (!ids.Contains(component.ProductComponentId))
            {
                ids.Add(component.ProductComponentId);
                names.Add(component.ComponentName);
            }
        }
        var warning = skipped.Count == 0 ? null : string.Format(Messages.OperationImportComponentsSkipped, string.Join("; ", skipped));
        return ids.Count == 0 && skipped.Count > 0 ? (null, names, warning) : (ids, names, warning);
    }

    /// <summary>Mô tả thay đổi nhóm / chi tiết của công đoạn đã có, null khi không đổi (DEV-ALL-116).</summary>
    private static string? ChangeNote(Operation existing, OperationExcelRowDto raw, OperationExcelSheetDto sheet, ImportRefs refs,
        IReadOnlyList<int>? componentIds)
    {
        var parts = new List<string>();
        if (sheet.HasGroupColumn)
        {
            var current = existing.OperationGroupId is { } gid ? refs.GroupNameById.GetValueOrDefault(gid) : null;
            if (!string.Equals(current?.Trim(), raw.GroupName, StringComparison.OrdinalIgnoreCase))
                parts.Add(string.Format(Messages.OperationImportGroupChange, current ?? Messages.OperationImportNone,
                    raw.GroupName ?? Messages.OperationImportNone));
        }
        if (componentIds is not null)
        {
            var current = refs.Links.Where(l => l.OperationId == existing.OperationId).Select(l => l.ProductComponentId).ToHashSet();
            if (!current.SetEquals(componentIds))
            {
                string Names(IEnumerable<int> ids) => string.Join(", ", refs.Components.Values.Where(c => ids.Contains(c.ProductComponentId))
                    .Select(c => c.ComponentName)) is { Length: > 0 } s ? s : Messages.OperationImportNone;
                parts.Add(string.Format(Messages.OperationImportComponentChange, Names(current), Names(componentIds)));
            }
        }
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    /// <summary>
    /// Quyết định cách xử lý đơn giá của một dòng theo ngày hiệu lực đã chọn: thêm bản mới, cập nhật bản Nháp / bị từ chối
    /// cùng ngày, không đổi (giá đã đúng), hoặc bỏ qua (cùng ngày đã gửi duyệt / đã duyệt với giá khác, công đoạn đã ngừng).
    /// </summary>
    private static (string Action, string? Note, PieceRate? Existing) PlanRate(
        OperationExcelRowDto raw, Operation? existing, DateOnly? effectiveDate,
        IReadOnlyDictionary<int, List<PieceRate>> ratesByOperation)
    {
        if (effectiveDate is null || raw.Rate is null) return (RateNone, null, null);
        if (existing is null) return (ActionNew, null, null);
        if (existing.Status != OperationStatuses.Active) return (RateSkipped, "Công đoạn đã ngừng dùng.", null);

        ratesByOperation.TryGetValue(existing.OperationId, out var own);
        own ??= [];

        var sameDate = own.FirstOrDefault(r => r.EffectiveDate == effectiveDate);
        if (sameDate is not null)
        {
            if (sameDate.Status is PieceRateStatuses.Draft or PieceRateStatuses.Rejected)
                return sameDate.Status == PieceRateStatuses.Draft && sameDate.Rate == raw.Rate
                    ? (ActionUnchanged, null, sameDate)
                    : (ActionUpdate, null, sameDate);
            return sameDate.Rate == raw.Rate
                ? (ActionUnchanged, null, sameDate)
                : (RateSkipped, "Ngày này đã có đơn giá chờ duyệt / đã duyệt với giá khác.", sameDate);
        }

        var current = own.Where(r => r.Status == PieceRateStatuses.Approved && r.EffectiveDate <= effectiveDate)
            .OrderByDescending(r => r.EffectiveDate).FirstOrDefault();
        return current?.Rate == raw.Rate ? (ActionUnchanged, "Bằng đơn giá hiện hành.", null) : (ActionNew, null, null);
    }

    private static bool IsSame(Operation existing, OperationExcelRowDto raw) =>
        existing.OperationName == raw.Name && existing.StationSequence == raw.Stt
        && existing.SkillLevel == raw.SkillLevel && existing.StandardTimeSec == raw.StandardTimeSec;

    private static void ApplyImported(Operation operation, OperationImportRowDto row)
    {
        operation.OperationCode = row.OperationCode;
        operation.OperationName = row.OperationName!;
        operation.StationSequence = (short)row.Stt;
        operation.SkillLevel = row.SkillLevel;
        operation.StandardTimeSec = row.StandardTimeSec;
    }

    private static object RateSnapshot(PieceRate r) => new { r.OperationId, r.Rate, r.EffectiveDate, r.Status };

    /// <summary>Kiểm tra mã / thời gian chuẩn trước khi Trim (tránh NRE khi OperationCode null).</summary>
    private static Result? ValidateSaveFields(SaveOperationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OperationCode))
            return Result.Failure(ErrorCodes.OperationInvalid, Messages.Required("mã công đoạn"), ResultKind.Validation);
        if (dto.OperationCode.Trim().Length > OperationLimits.MaxOperationCodeLength)
            return Result.Failure(ErrorCodes.OperationInvalid, Messages.OperationCodeTooLong, ResultKind.Validation);
        if (dto.StandardTimeSec is <= 0)
            return Result.Failure(ErrorCodes.OperationInvalid, Messages.OperationStandardTimeInvalid, ResultKind.Validation);
        return null;
    }

    /// <summary>Mã / STT trùng trong mã hàng — báo rõ trước khi DB chặn.</summary>
    private async Task<Result?> CheckAsync(SaveOperationDto dto, string code, Operation? existing, CancellationToken ct)
    {
        var exceptId = existing?.OperationId;
        var duplicate = await Operations.AsNoTracking().AnyAsync(
            o => o.ProductId == dto.ProductId && o.OperationCode == code && (exceptId == null || o.OperationId != exceptId), ct);
        if (duplicate) return Result.Failure(ErrorCodes.OperationCodeDuplicate, Messages.OperationCodeDuplicate);

        var duplicateSeq = await Operations.AsNoTracking().AnyAsync(
            o => o.ProductId == dto.ProductId && o.StationSequence == dto.StationSequence
                 && (exceptId == null || o.OperationId != exceptId), ct);
        if (duplicateSeq)
            return Result.Failure(ErrorCodes.OperationSequenceDuplicate, Messages.OperationSequenceDuplicate);

        return null;
    }

    /// <summary>
    /// Tick công đoạn cuối ở công đoạn này thì công đoạn cuối đang dùng trước đó tự bỏ tick. Lưu ngay, vì chỉ mục UX_Operation_FinalStep
    /// không cho hai dòng cùng là công đoạn cuối trong một lô lệnh, mà EF ghi theo thứ tự khóa chứ không theo thứ tự gán.
    /// </summary>
    private async Task<List<Operation>> MoveFinalStepAsync(int productId, int? exceptId, CancellationToken ct)
    {
        var others = await Operations.Where(o => o.ProductId == productId && o.IsFinalStep
            && o.Status == OperationStatuses.Active && (exceptId == null || o.OperationId != exceptId)).ToListAsync(ct);
        foreach (var other in others)
        {
            other.IsFinalStep = false;
            _audit.Add(new AuditEntry(nameof(Operation), other.OperationId, AuditActions.Update,
                new { IsFinalStep = true }, new { IsFinalStep = false }));
        }
        if (others.Count > 0) await _uow.SaveChangesAsync();
        return others;
    }

    /// <param name="alsoIgnore">Công đoạn sắp bị ngừng dùng trong cùng lần nhập (chế độ thay thế): không tính là công đoạn cuối đang có.</param>
    private Task<bool> FinalStepExistsAsync(int productId, int? exceptId, CancellationToken ct, IReadOnlyCollection<int>? alsoIgnore = null) =>
        Operations.AsNoTracking().AnyAsync(o => o.ProductId == productId && o.IsFinalStep
            && o.Status == OperationStatuses.Active && (exceptId == null || o.OperationId != exceptId)
            && (alsoIgnore == null || !alsoIgnore.Contains(o.OperationId)), ct);

    private static void Apply(Operation operation, SaveOperationDto dto, string code)
    {
        operation.OperationCode = code;
        operation.OperationName = dto.OperationName.Trim();
        operation.StationSequence = dto.StationSequence;
        operation.SkillLevel = dto.SkillLevel;
        operation.StandardTimeSec = dto.StandardTimeSec;
        operation.TeamType = string.IsNullOrWhiteSpace(dto.TeamType) ? TeamTypes.Sewing : CodeText.ToCode(dto.TeamType);
        // Công đoạn đã ngừng: không đổi / không nhận tick công đoạn cuối (MoveFinalStep cũng bỏ qua).
        if (operation.Status == OperationStatuses.Active)
            operation.IsFinalStep = dto.IsFinalStep;
    }

    /// <summary>
    /// Nhóm công đoạn (gõ tên, chưa có thì tạo cho mã hàng) và các chi tiết vải công đoạn làm trên đó (nhiều–nhiều). Chi tiết phải
    /// là PANEL của cùng mã hàng (DEV-ALL-86: vải của chi tiết tra qua định mức, mỗi chi tiết một vải ở một màu – size); danh sách trống = không gắn chi tiết nào. <c>ComponentIds</c> null = giữ nguyên (cập nhật từ import Excel).
    /// </summary>
    private async Task<Result?> ApplyGroupAndComponentsAsync(Operation operation, SaveOperationDto dto, CancellationToken ct)
    {
        var groupName = dto.GroupName?.Trim();
        if (string.IsNullOrEmpty(groupName))
        {
            operation.OperationGroup = null;
            operation.OperationGroupId = null;
        }
        else
        {
            var group = await _uow.Repository<OperationGroup>().Query()
                .FirstOrDefaultAsync(g => g.ProductId == operation.ProductId && g.GroupName == groupName, ct);
            if (group is null)
            {
                group = new OperationGroup { ProductId = operation.ProductId, GroupName = groupName };
                await _uow.Repository<OperationGroup>().AddAsync(group);
            }
            operation.OperationGroup = group;
        }

        if (dto.ComponentIds is null) return null;
        var wanted = dto.ComponentIds.Distinct().ToList();
        var valid = await _uow.Repository<ProductComponent>().Query().AsNoTracking()
            .CountAsync(c => wanted.Contains(c.ProductComponentId) && c.ProductId == operation.ProductId
                             && c.ComponentType == ComponentTypes.Panel, ct);
        if (valid != wanted.Count)
            return Result.Failure(ErrorCodes.OperationInvalid, Messages.OperationComponentInvalid, ResultKind.Validation);

        var current = operation.OperationId == 0
            ? new List<OperationComponent>()
            : await _uow.Repository<OperationComponent>().Query().Where(c => c.OperationId == operation.OperationId).ToListAsync(ct);
        _uow.Repository<OperationComponent>().RemoveRange(current.Where(c => !wanted.Contains(c.ProductComponentId)).ToList());
        foreach (var id in wanted.Where(id => current.All(c => c.ProductComponentId != id)))
            operation.OperationComponents.Add(new OperationComponent { ProductComponentId = id, ProductId = operation.ProductId });
        return null;
    }

    private static object Snapshot(Operation o) => new
    {
        o.ProductId, o.OperationCode, o.OperationName, o.StationSequence, o.SkillLevel, o.StandardTimeSec,
        o.TeamType, o.IsFinalStep, o.Status,
    };
}
