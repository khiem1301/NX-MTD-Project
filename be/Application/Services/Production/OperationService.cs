using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

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
        var items = await Operations.AsNoTracking()
            .Where(o => o.ProductId == productId && (includeDiscontinued || o.Status == OperationStatuses.Active))
            .OrderBy(o => o.StationSequence).ThenBy(o => o.OperationCode)
            .Select(o => new OperationDto(o.OperationId, o.ProductId, o.Product.ProductCode, o.OperationCode,
                o.OperationName, o.StationSequence, o.SkillLevel, o.StandardTimeSec, o.TeamType, o.IsFinalStep,
                o.Status, null, null))
            .ToListAsync(ct);

        // Một truy vấn lấy mọi đơn giá của mã hàng, rồi chọn đơn giá hiện hành / đơn giá sắp tới trong bộ nhớ.
        var today = _clock.Today;
        var rates = (await _uow.Repository<PieceRate>().Query().AsNoTracking()
                .Where(r => r.Operation.ProductId == productId)
                .Select(r => new { r.PieceRateId, r.OperationId, r.Rate, r.EffectiveDate, r.Status, r.RejectReason })
                .ToListAsync(ct))
            .GroupBy(r => r.OperationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<OperationDto>(items.Count);
        foreach (var item in items)
        {
            rates.TryGetValue(item.OperationId, out var own);
            own ??= [];

            // Giống FindEffectiveAsync: đơn giá đã duyệt có ngày hiệu lực lớn nhất không quá hôm nay.
            var current = own.Where(r => r.Status == PieceRateStatuses.Approved && r.EffectiveDate <= today)
                .OrderByDescending(r => r.EffectiveDate).FirstOrDefault();
            var latest = own.OrderByDescending(r => r.EffectiveDate).ThenByDescending(r => r.PieceRateId).FirstOrDefault();
            var next = latest is not null && latest.PieceRateId != current?.PieceRateId
                ? new OperationRateDto(latest.PieceRateId, latest.Rate, latest.EffectiveDate,
                    CodeText.ToPascal(latest.Status), latest.RejectReason)
                : null;

            result.Add(item with
            {
                TeamType = CodeText.ToPascal(item.TeamType), Status = CodeText.ToPascal(item.Status),
                CurrentRate = current?.Rate, NextRate = next,
            });
        }
        return Result<IReadOnlyList<OperationDto>>.Success(result);
    }

    public async Task<Result<int>> CreateAsync(SaveOperationDto dto, CancellationToken ct)
    {
        var product = await _uow.Repository<Product>().Query().AsNoTracking()
            .AnyAsync(p => p.ProductId == dto.ProductId, ct);
        if (!product) return Fail.NotFound<int>();

        var code = dto.OperationCode.Trim().ToUpperInvariant();
        var check = await CheckAsync(dto, code, null, ct);
        if (check is not null) return Fail.As<int>(check);

        var operation = new Operation { ProductId = dto.ProductId, Status = OperationStatuses.Active };
        Apply(operation, dto, code);

        return await _uow.InTransactionAsync(async () =>   // log cần OperationId nên lưu trước
        {
            await _uow.Repository<Operation>().AddAsync(operation);
            await _uow.SaveChangesAsync();
            _audit.Add(new AuditEntry(nameof(Operation), operation.OperationId, AuditActions.Insert, null, Snapshot(operation)));
            await _uow.SaveChangesAsync();
            return Result<int>.Success(operation.OperationId, Messages.Saved);
        });
    }

    public async Task<Result> UpdateAsync(int operationId, SaveOperationDto dto, CancellationToken ct)
    {
        var operation = await _uow.Repository<Operation>().GetByIdAsync(operationId);
        if (operation is null) return Fail.NotFound();

        var code = dto.OperationCode.Trim().ToUpperInvariant();
        // Mã hàng của công đoạn không đổi: sản lượng và đơn giá cũ gắn với mã hàng đó.
        var check = await CheckAsync(dto with { ProductId = operation.ProductId }, code, operation, ct);
        if (check is not null) return check;

        var before = Snapshot(operation);
        Apply(operation, dto, code);
        _audit.Add(new AuditEntry(nameof(Operation), operationId, AuditActions.Update, before, Snapshot(operation)));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
    }

    public async Task<Result> DiscontinueAsync(int operationId, CancellationToken ct)
    {
        var operation = await _uow.Repository<Operation>().GetByIdAsync(operationId);
        if (operation is null) return Fail.NotFound();
        if (operation.Status == OperationStatuses.Discontinued) return Result.Success(Messages.Saved);

        operation.Status = OperationStatuses.Discontinued;
        _audit.Add(new AuditEntry(nameof(Operation), operationId, AuditActions.Update,
            new { Status = OperationStatuses.Active }, new { operation.Status }));
        await _uow.SaveChangesAsync();
        return Result.Success(Messages.Saved);
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

    private sealed record PlannedRow(
        OperationImportRowDto Preview, OperationExcelRowDto Raw, Operation? Existing, PieceRate? ExistingRate);

    private sealed record ImportPlan(
        OperationExcelSheetDto Sheet, IReadOnlyList<string> SheetNames, bool ProductHasFinalStep, IReadOnlyList<PlannedRow> Rows);

    private const string ActionNew = "New";
    private const string ActionUpdate = "Update";
    private const string ActionUnchanged = "Unchanged";
    private const string ActionError = "Error";
    private const string RateNone = "None";
    private const string RateSkipped = "Skipped";

    /// <summary>Mã công đoạn sinh từ số thứ tự trong file: STT 7 → "OP07". Nhập lại cùng file thì khớp đúng công đoạn cũ.</summary>
    private static string CodeFor(int stt) => $"OP{stt:00}";

    public async Task<Result<OperationImportPreviewDto>> PreviewImportAsync(
        int productId, Stream stream, string? sheetName, DateOnly? effectiveDate, CancellationToken ct)
    {
        var plan = await BuildImportPlanAsync(productId, stream, sheetName, effectiveDate, ct);
        if (!plan.IsSuccess) return Fail.As<OperationImportPreviewDto>(plan);

        var p = plan.Data!;
        int Count(string action) => p.Rows.Count(r => r.Preview.Action == action);
        int RateCount(string action) => p.Rows.Count(r => r.Preview.RateAction == action);
        return Result<OperationImportPreviewDto>.Success(new OperationImportPreviewDto(
            p.Sheet.SheetName, p.SheetNames, p.Sheet.ProductText, Count(ActionNew), Count(ActionUpdate),
            Count(ActionUnchanged), Count(ActionError), p.ProductHasFinalStep, p.Sheet.HasRateColumn,
            RateCount(ActionNew), RateCount(ActionUpdate), RateCount(ActionUnchanged), RateCount(RateSkipped),
            p.Rows.Select(r => r.Preview).ToList()));
    }

    public async Task<Result<OperationImportResultDto>> ImportAsync(
        int productId, Stream stream, string? sheetName, int? finalStt, DateOnly? effectiveDate, bool submitRates,
        CancellationToken ct)
    {
        var plan = await BuildImportPlanAsync(productId, stream, sheetName, effectiveDate, ct);
        if (!plan.IsSuccess) return Fail.As<OperationImportResultDto>(plan);

        var valid = plan.Data!.Rows.Where(r => r.Preview.Action != ActionError).ToList();
        if (valid.Count == 0)
            return Result<OperationImportResultDto>.Failure(ErrorCodes.OperationExcelEmpty, Messages.OperationExcelEmpty,
                ResultKind.Validation);
        var skipped = plan.Data.Rows.Count - valid.Count;

        if (finalStt is not null)
        {
            var target = valid.FirstOrDefault(r => r.Preview.Stt == finalStt);
            if (target is null)
                return Result<OperationImportResultDto>.Failure(ErrorCodes.OperationImportFinalNotFound,
                    Messages.OperationImportFinalNotFound, ResultKind.Validation);
            if (await FinalStepExistsAsync(productId, target.Existing?.OperationId, ct))
                return Result<OperationImportResultDto>.Failure(ErrorCodes.OperationFinalStepExists,
                    Messages.OperationFinalStepExists);
        }

        return await _uow.InTransactionAsync(async () =>
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

            if (created.Count > 0) await _uow.Repository<Operation>().AddRangeAsync(created);
            await _uow.SaveChangesAsync();   // lấy OperationId của dòng mới để ghi nhật ký và gắn đơn giá

            foreach (var operation in created)
                _audit.Add(new AuditEntry(nameof(Operation), operation.OperationId, AuditActions.Insert, null, Snapshot(operation)));
            foreach (var (operation, before) in updated)
                _audit.Add(new AuditEntry(nameof(Operation), operation.OperationId, AuditActions.Update, before, Snapshot(operation)));

            var rateStats = await ApplyImportedRatesAsync(valid, operationByStt, effectiveDate, submitRates);
            await _uow.SaveChangesAsync();

            if (rateStats.Submitted > 0)
                await _notifier.NotifyRoleAsync(RoleCodes.Admin, new NotificationMessage(NotificationSources.Production,
                    Messages.NotifyPieceRateSubmittedTitle,
                    string.Format(Messages.NotifyPieceRatesImportedBody, plan.Data.Sheet.SheetName, rateStats.Submitted),
                    null, null, null), ct);

            var result = new OperationImportResultDto(created.Count, updated.Count, unchanged, skipped,
                rateStats.Created, rateStats.Updated, rateStats.Unchanged, rateStats.Skipped);
            return Result<OperationImportResultDto>.Success(result, string.Format(Messages.OperationImportDone,
                created.Count, updated.Count, unchanged, skipped, rateStats.Created, rateStats.Updated,
                rateStats.Unchanged, rateStats.Skipped));
        });
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
                    OperationId = operation.OperationId, Rate = row.Raw.Rate.Value, EffectiveDate = effectiveDate.Value,
                    Status = status, SubmittedAt = submit ? now : null,
                });
                continue;
            }

            var rate = row.ExistingRate!;
            var before = RateSnapshot(rate);
            rate.Rate = row.Raw.Rate.Value;
            rate.Status = status;
            rate.RejectReason = null;
            rate.SubmittedAt = submit ? now : null;
            updated.Add((rate, before));
        }

        if (created.Count > 0) await _uow.Repository<PieceRate>().AddRangeAsync(created);
        await _uow.SaveChangesAsync();   // lấy PieceRateId để ghi nhật ký

        foreach (var rate in created)
            _audit.Add(new AuditEntry(nameof(PieceRate), rate.PieceRateId, AuditActions.Insert, null, RateSnapshot(rate)));
        foreach (var (rate, before) in updated)
            _audit.Add(new AuditEntry(nameof(PieceRate), rate.PieceRateId, AuditActions.Update, before, RateSnapshot(rate)));

        return new RateStats(created.Count, updated.Count, unchanged, skipped, submit ? created.Count + updated.Count : 0);
    }

    private async Task<Result<ImportPlan>> BuildImportPlanAsync(
        int productId, Stream stream, string? sheetName, DateOnly? effectiveDate, CancellationToken ct)
    {
        var productExists = await _uow.Repository<Product>().Query().AsNoTracking().AnyAsync(p => p.ProductId == productId, ct);
        if (!productExists) return Fail.NotFound<ImportPlan>();

        IReadOnlyList<OperationExcelSheetDto> sheets;
        try
        {
            sheets = _excel.Parse(stream);
        }
        catch (InvalidDataException ex)
        {
            return Result<ImportPlan>.Failure(ErrorCodes.OperationExcelInvalid, ex.Message, ResultKind.Validation);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException
                                       or InvalidOperationException or NotImplementedException)
        {
            return Result<ImportPlan>.Failure(ErrorCodes.OperationExcelInvalid, Messages.OperationExcelInvalid,
                ResultKind.Validation);
        }

        var sheet = string.IsNullOrWhiteSpace(sheetName)
            ? sheets[0]
            : sheets.FirstOrDefault(s => string.Equals(s.SheetName, sheetName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (sheet is null)
            return Result<ImportPlan>.Failure(ErrorCodes.OperationImportSheetNotFound, Messages.OperationImportSheetNotFound,
                ResultKind.Validation);

        var existingOps = await Operations.Where(o => o.ProductId == productId).ToListAsync(ct);
        var byCode = existingOps.ToDictionary(o => o.OperationCode, StringComparer.OrdinalIgnoreCase);
        var ratesByOperation = (await _uow.Repository<PieceRate>().Query().Where(r => r.Operation.ProductId == productId)
                .ToListAsync(ct))
            .GroupBy(r => r.OperationId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var seenStt = new HashSet<int>();

        var rows = new List<PlannedRow>(sheet.Rows.Count);
        foreach (var raw in sheet.Rows)
        {
            var code = CodeFor(raw.Stt);
            var error = raw.ParseError;
            if (error is null && !seenStt.Add(raw.Stt)) error = $"Trùng STT {raw.Stt} trong file.";

            if (error is not null)
            {
                rows.Add(new PlannedRow(new OperationImportRowDto(raw.RowNumber, raw.Stt, code, raw.Name, raw.SkillLevel,
                    raw.StandardTimeSec, raw.Equipment, raw.Rate, ActionError, error), raw, null, null));
                continue;
            }

            byCode.TryGetValue(code, out var existing);
            var action = existing is null ? ActionNew : IsSame(existing, raw) ? ActionUnchanged : ActionUpdate;
            var (rateAction, rateNote, existingRate) = PlanRate(raw, existing, effectiveDate, ratesByOperation);
            rows.Add(new PlannedRow(new OperationImportRowDto(raw.RowNumber, raw.Stt, code, raw.Name, raw.SkillLevel,
                raw.StandardTimeSec, raw.Equipment, raw.Rate, action, null, rateAction, rateNote), raw, existing, existingRate));
        }

        var hasFinal = existingOps.Any(o => o.IsFinalStep && o.Status == OperationStatuses.Active);
        return Result<ImportPlan>.Success(new ImportPlan(sheet, sheets.Select(s => s.SheetName).ToList(), hasFinal, rows));
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

    /// <summary>Mã trùng trong mã hàng và "đúng một công đoạn may cuối đang dùng" — báo rõ trước khi DB chặn.</summary>
    private async Task<Result?> CheckAsync(SaveOperationDto dto, string code, Operation? existing, CancellationToken ct)
    {
        var exceptId = existing?.OperationId;
        var duplicate = await Operations.AsNoTracking().AnyAsync(
            o => o.ProductId == dto.ProductId && o.OperationCode == code && (exceptId == null || o.OperationId != exceptId), ct);
        if (duplicate) return Result.Failure(ErrorCodes.OperationCodeDuplicate, Messages.OperationCodeDuplicate);

        var willBeActive = existing is null || existing.Status == OperationStatuses.Active;
        if (dto.IsFinalStep && willBeActive && await FinalStepExistsAsync(dto.ProductId, exceptId, ct))
            return Result.Failure(ErrorCodes.OperationFinalStepExists, Messages.OperationFinalStepExists);
        return null;
    }

    private Task<bool> FinalStepExistsAsync(int productId, int? exceptId, CancellationToken ct) =>
        Operations.AsNoTracking().AnyAsync(o => o.ProductId == productId && o.IsFinalStep
            && o.Status == OperationStatuses.Active && (exceptId == null || o.OperationId != exceptId), ct);

    private static void Apply(Operation operation, SaveOperationDto dto, string code)
    {
        operation.OperationCode = code;
        operation.OperationName = dto.OperationName.Trim();
        operation.StationSequence = dto.StationSequence;
        operation.SkillLevel = dto.SkillLevel;
        operation.StandardTimeSec = dto.StandardTimeSec;
        operation.TeamType = string.IsNullOrWhiteSpace(dto.TeamType) ? TeamTypes.Sewing : CodeText.ToCode(dto.TeamType);
        operation.IsFinalStep = dto.IsFinalStep;
    }

    private static object Snapshot(Operation o) => new
    {
        o.ProductId, o.OperationCode, o.OperationName, o.StationSequence, o.SkillLevel, o.StandardTimeSec,
        o.TeamType, o.IsFinalStep, o.Status,
    };
}
