using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-148: AI gợi ý bậc thợ cho cả tổ. Chỉ gửi AI số liệu đã ẩn danh (khóa tạm W1, W2..., không tên / mã / tiền);
/// bản gợi ý lưu tạm theo tổ (<see cref="IGradeSuggestionStore"/>), tổ trưởng sửa thì bản lưu đổi theo. Xác nhận: chỉ dòng
/// đổi bậc mới thành <c>GradeEvaluation</c> SUBMITTED; công nhân đang có đề xuất chờ quyết định thì thay đề xuất đó.
/// </summary>
public sealed class GradeSuggestionService : IGradeSuggestionService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IGradeAdvisor _advisor;
    private readonly IGradeSuggestionStore _store;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly GradeSuggestionOptions _options;

    public GradeSuggestionService(IUnitOfWork uow, ICurrentUser user, IClock clock, IGradeAdvisor advisor, IGradeSuggestionStore store,
        INotificationService notifier, IAuditLogService audit, IOptions<GradeSuggestionOptions> options)
    {
        _uow = uow; _user = user; _clock = clock; _advisor = advisor; _store = store; _notifier = notifier; _audit = audit;
        _options = options.Value;
    }

    private IQueryable<GradeEvaluation> Evaluations => _uow.Repository<GradeEvaluation>().Query();
    private IQueryable<Employee> Employees => _uow.Repository<Employee>().Query();

    private sealed record TeamWorker(int EmployeeId, string EmployeeCode, string FullName, string? CurrentGradeCode, DateOnly StartDate);

    public async Task<Result<GradeSuggestionDto?>> GetAsync(int teamId, CancellationToken ct)
    {
        var draft = await LoadAsync(teamId, ct);
        return Result<GradeSuggestionDto?>.Success(draft is null ? null : await WithLiveStatusAsync(draft, ct));
    }

    public async Task<Result<GradeSuggestionDto>> GenerateAsync(int teamId, GenerateGradeSuggestionDto dto, CancellationToken ct)
    {
        if (!_advisor.IsConfigured) return Result<GradeSuggestionDto>.Failure(ErrorCodes.GradeAiUnavailable, Messages.GradeAiNotConfigured);
        var workers = await TeamWorkersAsync(teamId, ct);
        if (workers.Count == 0) return Result<GradeSuggestionDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeAiNoWorkers);

        var to = _clock.Today;
        var from = to.AddDays(1 - dto.LookbackDays);
        var metrics = await new GradeMetricsReader(_uow)
            .ReadAsync(workers.Select(w => new GradeMetricsReader.WorkerRef(w.EmployeeId, w.StartDate)).ToList(), from, to, ct);
        var grades = await RankedGradesAsync(to, ct);
        if (grades.Count == 0) return Result<GradeSuggestionDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeCodeInvalid);
        var keyed = workers.Select((w, i) => (Key: $"W{i + 1}", Worker: w)).ToList();
        var advice = await _advisor.SuggestAsync(new GradeAdvisorRequest(dto.LookbackDays, grades,
            keyed.Select(k => new GradeAdvisorWorker(k.Key, k.Worker.CurrentGradeCode, metrics[k.Worker.EmployeeId])).ToList()), ct);
        if (advice is null) return Result<GradeSuggestionDto>.Failure(ErrorCodes.GradeAiUnavailable, Messages.GradeAiFailed);

        var valid = grades.Select(g => g.Code).ToHashSet();
        var items = keyed.Select(k => ToItem(k.Worker, metrics[k.Worker.EmployeeId],
            advice.FirstOrDefault(a => a.Key == k.Key && valid.Contains(a.GradeCode)))).ToList();
        var now = _clock.Now;
        var draft = new GradeSuggestionDto(teamId, from, to, now, now.AddHours(_options.RetentionHours), await CurrentUserNameAsync(ct), null, items);
        await _store.SaveAsync(draft, ct);
        return Result<GradeSuggestionDto>.Success(await WithLiveStatusAsync(draft, ct), Messages.GradeAiGenerated);
    }

    public async Task<Result<GradeSuggestionDto>> UpdateItemAsync(int teamId, int workerId, UpdateGradeSuggestionItemDto dto, CancellationToken ct)
    {
        var code = dto.ProposedGradeCode.Trim().ToUpperInvariant();
        if (!await _uow.Repository<GradeLevelCode>().Query().AnyAsync(c => c.GradeCode == code && c.Status == GradeCodes.Active, ct))
            return Result<GradeSuggestionDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeCodeInvalid, ResultKind.Validation);
        var draft = await LoadAsync(teamId, ct);
        if (draft is null) return Result<GradeSuggestionDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeAiNoDraft, ResultKind.NotFound);
        var item = draft.Items.FirstOrDefault(i => i.WorkerId == workerId);
        if (item is null) return Result<GradeSuggestionDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeAiWorkerNotInDraft, ResultKind.NotFound);

        var reason = dto.Reason.Trim();
        var updated = item with { ProposedGradeCode = code, Reason = reason, IsEdited = code != item.AiGradeCode || reason != item.AiReason };
        draft = draft with { Items = draft.Items.Select(i => i.WorkerId == workerId ? updated : i).ToList() };
        await _store.SaveAsync(draft, ct);
        return Result<GradeSuggestionDto>.Success(await WithLiveStatusAsync(draft, ct), Messages.GradeAiItemSaved);
    }

    public async Task<Result<GradeSuggestionDto>> RemoveItemAsync(int teamId, int workerId, CancellationToken ct)
    {
        var draft = await LoadAsync(teamId, ct);
        if (draft is null) return Result<GradeSuggestionDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeAiNoDraft, ResultKind.NotFound);
        if (draft.Items.All(i => i.WorkerId != workerId))
            return Result<GradeSuggestionDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeAiWorkerNotInDraft, ResultKind.NotFound);

        draft = draft with { Items = draft.Items.Where(i => i.WorkerId != workerId).ToList() };
        await _store.SaveAsync(draft, ct);
        return Result<GradeSuggestionDto>.Success(await WithLiveStatusAsync(draft, ct), Messages.GradeAiItemRemoved);
    }

    public async Task<Result<GradeSuggestionConfirmDto>> ConfirmAsync(int teamId, CancellationToken ct)
    {
        var draft = await LoadAsync(teamId, ct);
        if (draft is null) return Result<GradeSuggestionConfirmDto>.Failure(ErrorCodes.GradeInvalid, Messages.GradeAiNoDraft, ResultKind.NotFound);

        var result = await _uow.InTransactionAsync(() => SaveProposalsAsync(draft, ct));
        if (result.IsSuccess && result.Data!.Created + result.Data.Replaced > 0)
            await _store.SaveAsync(draft with { ConfirmedAt = _clock.Now }, ct);
        return result;
    }

    // ---------- Xác nhận ----------

    private sealed class ConfirmTally
    {
        public int Created;
        public int Replaced;
        public int Unchanged;
        public List<string> Skipped { get; } = new();
        public GradeSuggestionConfirmDto ToDto() => new(Created, Replaced, Unchanged, Skipped);
    }

    private async Task<Result<GradeSuggestionConfirmDto>> SaveProposalsAsync(GradeSuggestionDto draft, CancellationToken ct)
    {
        var ids = draft.Items.Select(i => i.WorkerId).ToList();
        var today = _clock.Today;
        var workers = await Employees.AsNoTracking()
            .Where(e => ids.Contains(e.EmployeeId) && e.TeamId == draft.TeamId && e.Status == RecordStatus.Active && e.Role.RoleCode == RoleCodes.Worker)
            .ToDictionaryAsync(e => e.EmployeeId, ct);
        var evals = await Evaluations.Where(e => ids.Contains(e.WorkerId)
            && (e.Status == GradeEvaluationStatuses.Submitted || (e.ProposedBy == _user.EmployeeId && e.EvaluationDate == today))).ToListAsync(ct);
        var active = (await _uow.Repository<GradeLevelCode>().Query().Where(c => c.Status == GradeCodes.Active)
            .Select(c => c.GradeCode).ToListAsync(ct)).ToHashSet();
        var defects = await GradeDefectCounter.CountRecentAsync(_uow, today, ids, ct);

        var tally = new ConfirmTally();
        foreach (var item in draft.Items)
            await ApplyItemAsync(item, draft.TeamId, workers.GetValueOrDefault(item.WorkerId), evals, active, defects, tally);

        var sent = tally.Created + tally.Replaced;
        if (sent == 0) return Result<GradeSuggestionConfirmDto>.Success(tally.ToDto(), Messages.GradeAiNoChange);
        await _notifier.NotifyRoleAsync(RoleCodes.Admin, new NotificationMessage(NotificationSources.System, Messages.GradeAiNotifyTitle,
            string.Format(Messages.GradeAiNotifyBody, sent), null), ct);
        await _uow.SaveChangesAsync();
        return Result<GradeSuggestionConfirmDto>.Success(tally.ToDto(), string.Format(Messages.GradeAiConfirmed, sent, tally.Created, tally.Replaced));
    }

    private async Task ApplyItemAsync(GradeSuggestionItemDto item, int teamId, Employee? worker, List<GradeEvaluation> evals,
        HashSet<string> activeCodes, Dictionary<int, int> defects, ConfirmTally tally)
    {
        var label = $"{item.WorkerCode} - {item.FullName}";
        if (worker is null) { tally.Skipped.Add(string.Format(Messages.GradeAiSkipLeftTeam, label)); return; }
        if (item.ProposedGradeCode is null || item.ProposedGradeCode == worker.CurrentGradeCode) { tally.Unchanged++; return; }
        if (!activeCodes.Contains(item.ProposedGradeCode)) { tally.Skipped.Add(string.Format(Messages.GradeAiSkipInvalidGrade, label)); return; }

        var today = _clock.Today;
        var pending = evals.FirstOrDefault(e => e.WorkerId == worker.EmployeeId && e.Status == GradeEvaluationStatuses.Submitted);
        // Ràng buộc UQ_GradeEval (công nhân, người đề xuất, ngày): đề xuất khác của mình hôm nay đã được quyết định thì bỏ qua.
        if (evals.Any(e => e.WorkerId == worker.EmployeeId && e.ProposedBy == _user.EmployeeId && e.EvaluationDate == today && e != pending))
        {
            tally.Skipped.Add(string.Format(Messages.GradeAiSkipProposedToday, label));
            return;
        }

        var defectCount = defects.GetValueOrDefault(worker.EmployeeId);
        if (pending is not null)
        {
            ReplacePending(pending, worker, item, teamId, defectCount);
            tally.Replaced++;
            return;
        }
        await _uow.Repository<GradeEvaluation>().AddAsync(new GradeEvaluation
        {
            WorkerId = worker.EmployeeId, ProposedBy = _user.EmployeeId, TeamId = teamId, CurrentGradeCode = worker.CurrentGradeCode,
            ProposedGradeCode = item.ProposedGradeCode, Reason = item.Reason, RecentDefectCount = defectCount, EvaluationDate = today,
            Status = GradeEvaluationStatuses.Submitted, CreatedAt = _clock.Now,
        });
        tally.Created++;
    }

    /// <summary>Đề xuất đang chờ chưa được quyết định nên sửa được (GBR-22 chỉ khóa dòng đã quyết định); ghi AuditLog giá trị cũ / mới.</summary>
    private void ReplacePending(GradeEvaluation pending, Employee worker, GradeSuggestionItemDto item, int teamId, int defectCount)
    {
        var before = new { pending.ProposedGradeCode, pending.Reason, pending.ProposedBy, pending.EvaluationDate };
        pending.ProposedGradeCode = item.ProposedGradeCode!;
        pending.Reason = item.Reason;
        pending.ProposedBy = _user.EmployeeId;
        pending.TeamId = teamId;
        pending.CurrentGradeCode = worker.CurrentGradeCode;
        pending.RecentDefectCount = defectCount;
        pending.EvaluationDate = _clock.Today;
        _audit.Add(new AuditEntry(nameof(GradeEvaluation), pending.GradeEvaluationId, AuditActions.Update, before,
            new { pending.ProposedGradeCode, pending.Reason, pending.ProposedBy, pending.EvaluationDate }, Messages.GradeAiReplacedAudit));
    }

    // ---------- Nội bộ ----------

    private async Task<GradeSuggestionDto?> LoadAsync(int teamId, CancellationToken ct)
    {
        var draft = await _store.GetAsync(teamId, ct);
        if (draft is null || draft.ExpiresAt > _clock.Now) return draft;
        await _store.DeleteAsync(teamId, ct);
        return null;
    }

    /// <summary>Bậc đang dùng xếp theo tiền bậc hiện hành giảm dần (bậc chưa có tiền xếp cuối), để AI biết thứ hạng khi chỉ có mã bậc.</summary>
    private async Task<List<GradeAdvisorGrade>> RankedGradesAsync(DateOnly today, CancellationToken ct)
    {
        var codes = await _uow.Repository<GradeLevelCode>().Query().AsNoTracking()
            .Where(c => c.Status == GradeCodes.Active).Select(c => c.GradeCode).ToListAsync(ct);
        var amounts = (await _uow.Repository<GradeLevel>().Query().AsNoTracking()
                .Where(l => codes.Contains(l.GradeCode) && l.EffectiveDate <= today).ToListAsync(ct))
            .GroupBy(l => l.GradeCode)
            .ToDictionary(g => g.Key, g => g.MaxBy(l => l.EffectiveDate)!.MonthlyAmount);
        return codes.OrderByDescending(c => amounts.TryGetValue(c, out var a) ? a : decimal.MinValue).ThenBy(c => c)
            .Select((c, i) => new GradeAdvisorGrade(c, i + 1)).ToList();
    }

    private Task<List<TeamWorker>> TeamWorkersAsync(int teamId, CancellationToken ct) =>
        Employees.AsNoTracking()
            .Where(e => e.TeamId == teamId && e.Status == RecordStatus.Active && e.Role.RoleCode == RoleCodes.Worker)
            .OrderBy(e => e.EmployeeCode)
            .Select(e => new TeamWorker(e.EmployeeId, e.EmployeeCode, e.FullName, e.CurrentGradeCode, e.StartDate))
            .ToListAsync(ct);

    private async Task<string> CurrentUserNameAsync(CancellationToken ct) =>
        await Employees.AsNoTracking().Where(e => e.EmployeeId == _user.EmployeeId).Select(e => e.FullName).FirstOrDefaultAsync(ct) ?? string.Empty;

    private static GradeSuggestionItemDto ToItem(TeamWorker w, GradeWorkerMetricsDto metrics, GradeAdvice? advice)
    {
        var text = advice?.Reason.Trim();
        var reason = string.IsNullOrEmpty(text) ? Messages.GradeAiMissingAdvice : text[..Math.Min(text.Length, TextLimits.Reason)];
        return new GradeSuggestionItemDto(w.EmployeeId, w.EmployeeCode, w.FullName, w.CurrentGradeCode, null, metrics,
            advice?.GradeCode, reason, advice?.GradeCode ?? w.CurrentGradeCode, reason, false);
    }

    /// <summary>Bậc hiện tại và đề xuất đang chờ lấy lại từ DB mỗi lần đọc (có thể đã đổi sau khi tạo gợi ý).</summary>
    private async Task<GradeSuggestionDto> WithLiveStatusAsync(GradeSuggestionDto draft, CancellationToken ct)
    {
        var ids = draft.Items.Select(i => i.WorkerId).ToList();
        var current = await Employees.AsNoTracking().Where(e => ids.Contains(e.EmployeeId))
            .Select(e => new { e.EmployeeId, e.CurrentGradeCode }).ToDictionaryAsync(e => e.EmployeeId, e => e.CurrentGradeCode, ct);
        var pending = await Evaluations.AsNoTracking().Where(e => ids.Contains(e.WorkerId) && e.Status == GradeEvaluationStatuses.Submitted)
            .Select(e => new { e.WorkerId, e.ProposedGradeCode }).ToListAsync(ct);
        return draft with
        {
            Items = draft.Items.Select(i => i with
            {
                CurrentGradeCode = current.TryGetValue(i.WorkerId, out var grade) ? grade : i.CurrentGradeCode,
                PendingGradeCode = pending.FirstOrDefault(p => p.WorkerId == i.WorkerId)?.ProposedGradeCode,
            }).ToList(),
        };
    }
}
