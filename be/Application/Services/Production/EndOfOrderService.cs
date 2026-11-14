using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Kết thúc đơn của một tổ may (chỉ đánh giá khi đến cuối đơn, không theo ngày / tháng):
/// 1) với MỌI công đoạn, so sản lượng công nhân báo ở từng dòng đơn với QC đạt + hàng hủy (mỗi sản phẩm qua mỗi công đoạn đúng một
///    lần nên một công đoạn không thể có nhiều hơn mức đó); dòng nào báo nhiều hơn là LỆCH. Hệ thống KHÔNG tự ghi phiếu hay trừ
///    lương (DEV-ALL-107): chỉ cảnh báo trong màn kết thúc đơn và thông báo tóm tắt Quản đốc, tổ trưởng; chi tiết (công đoạn, dòng
///    đơn, những ai đã báo và sản lượng) xem ở cửa sổ chi tiết, để xác minh rồi ghi phiếu lỗi cho đúng người nếu cần;
/// 2) chốt KPI (đạt / không đạt theo số QC đạt cả đơn) và đóng chỉ tiêu;
/// 3) KPI không đạt: báo Quản đốc và tổ trưởng kèm % thiếu — thiếu không quá ngưỡng thì nên tăng ca, thiếu nhiều hơn thì
///    tổ trưởng quyết định (DB 29/10 không còn loại đơn tăng ca nên chỉ thông báo, không tạo đơn).
/// </summary>
public sealed class EndOfOrderService : IEndOfOrderService
{
    public const int DefaultThresholdPct = 10;

    /// <summary>Thông báo lệch chỉ nêu tối đa số công đoạn lệch nhiều nhất này, còn lại ghi "và N công đoạn khác".</summary>
    public const int MaxOperationsInNote = 3;

    /// <summary>Mỗi công đoạn trong thông báo chỉ nêu tối đa số người báo nhiều nhất này, còn lại ghi "và N người khác".</summary>
    public const int MaxWorkersInNote = 2;

    /// <summary>Notification.Message tối đa 1000 ký tự (cột NVARCHAR(1000)); số công đoạn / người đã bị chặn nên chỉ cắt khi tên dài bất thường.</summary>
    private const int MaxNotificationLength = 1000;

    // Link của thông báo "KPI không đạt": Quản đốc mở màn chỉ tiêu KPI, tổ trưởng / tổ phó về trang chủ (khung KPI của tổ).
    private const string ManagerKpiLink = "/kpi/targets";
    private const string TeamLeaderKpiLink = "/";

    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly ISystemSettingReader _settings;

    public EndOfOrderService(IUnitOfWork uow, INotificationService notifier, IAuditLogService audit, ISystemSettingReader settings)
    {
        _uow = uow; _notifier = notifier; _audit = audit; _settings = settings;
    }

    public async Task<Result<EndOfOrderPreviewDto>> PreviewAsync(int kpiTargetId, int? teamId, CancellationToken ct)
    {
        var target = await FindTargetAsync(kpiTargetId, ct);
        if (target is null || (teamId is not null && target.SewingTeamId != teamId)) return Fail.NotFound<EndOfOrderPreviewDto>();
        return Result<EndOfOrderPreviewDto>.Success(await BuildAsync(target, ct));
    }

    public async Task<Result<EndOfOrderPreviewDto>> FinishAsync(int kpiTargetId, CancellationToken ct)
    {
        var target = await _uow.Repository<KpiTarget>().Query().Include(k => k.Order).Include(k => k.Product).Include(k => k.SewingTeam)
            .FirstOrDefaultAsync(k => k.KpiTargetId == kpiTargetId, ct);
        if (target is null) return Fail.NotFound<EndOfOrderPreviewDto>();
        if (target.Status != KpiStatuses.Active)
            return Result<EndOfOrderPreviewDto>.Failure(ErrorCodes.KpiInvalidStatus, Messages.KpiInvalidStatusForFinish);

        var preview = await BuildAsync(target, ct);
        if (preview.Blockers.Count > 0)
            return Result<EndOfOrderPreviewDto>.Failure(ErrorCodes.KpiFinishBlocked, string.Join(" ", preview.Blockers));

        var result = await _uow.InTransactionAsync(async () =>
        {
            var before = new { target.Status, target.Result };
            target.Result = preview.Achieved ? KpiResults.Achieved : KpiResults.NotAchieved;
            target.Status = KpiStatuses.Closed;
            _audit.Add(new AuditEntry(nameof(KpiTarget), target.KpiTargetId, AuditActions.Update, before,
                new { target.Status, target.Result, ActualQty = preview.ActualQty, MismatchOperations = preview.Operations.Count },
                "Kết thúc đơn của tổ"));

            if (preview.Operations.Count > 0)
                await NotifyMismatchAsync(target, preview, ct);
            if (preview.OtDecision != OvertimeDecisions.None)
                await NotifyShortfallAsync(target, preview, ct);

            await _uow.SaveChangesAsync();
            return Result.Success();
        });
        if (!result.IsSuccess) return Fail.As<EndOfOrderPreviewDto>(result);
        return Result<EndOfOrderPreviewDto>.Success(preview, Messages.KpiFinished);
    }

    // ---------- Tính toán ----------

    private async Task<KpiTarget?> FindTargetAsync(int id, CancellationToken ct) =>
        await _uow.Repository<KpiTarget>().Query().AsNoTracking().Include(k => k.Order).Include(k => k.Product).Include(k => k.SewingTeam)
            .FirstOrDefaultAsync(k => k.KpiTargetId == id, ct);

    private async Task<EndOfOrderPreviewDto> BuildAsync(KpiTarget target, CancellationToken ct)
    {
        var threshold = await _settings.GetIntAsync(SettingKeys.KpiOtAutoThresholdPct, DefaultThresholdPct, ct);

        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking().Include(l => l.ProductVariant).ThenInclude(v => v.ProductColor)
            .Where(l => l.OrderId == target.OrderId && l.ProductId == target.ProductId).ToListAsync(ct);
        var lineIds = lines.Select(l => l.OrderLineId).ToList();

        var (qcByLine, scrapByLine) = await LoadQcAndScrapAsync(target, lineIds, ct);
        var actual = qcByLine.Values.Sum() - await QcOfOtherTargetsAsync(target, lineIds, ct);
        var operations = await BuildMismatchAsync(target, lines, qcByLine, scrapByLine, ct);

        var achieved = actual >= target.TargetFinishedQty;
        var shortfall = Math.Max(0, target.TargetFinishedQty - actual);
        var pct = target.TargetFinishedQty > 0 ? Math.Round(100m * shortfall / target.TargetFinishedQty, 2) : 0m;
        var ot = achieved ? OvertimeDecisions.None : pct <= threshold ? OvertimeDecisions.AutoApproved : OvertimeDecisions.LeaderDecides;

        return new EndOfOrderPreviewDto(target.KpiTargetId, target.Order.OrderCode, target.Product.ProductCode,
            target.SewingTeam.TeamName, target.TargetFinishedQty, actual, achieved, shortfall, pct, threshold, ot,
            operations, [], target.Status == KpiStatuses.Active);
    }

    /// <summary>QC đạt nằm trong kỳ của các chỉ tiêu khác cùng đơn + mã hàng + tổ: đã thuộc về chỉ tiêu đó, không tính lặp vào chỉ tiêu này.</summary>
    private async Task<int> QcOfOtherTargetsAsync(KpiTarget target, List<int> lineIds, CancellationToken ct)
    {
        var others = await _uow.Repository<KpiTarget>().Query().AsNoTracking()
            .Where(k => k.OrderId == target.OrderId && k.ProductId == target.ProductId && k.SewingTeamId == target.SewingTeamId
                        && k.Status != KpiStatuses.Cancelled && k.KpiTargetId != target.KpiTargetId)
            .Select(k => new { k.StartDate, k.EndDate }).ToListAsync(ct);
        if (others.Count == 0) return 0;

        var batches = await _uow.Repository<QcBatch>().Query().AsNoTracking()
            .Where(q => q.SewingTeamId == target.SewingTeamId && lineIds.Contains(q.OrderLineId))
            .Select(q => new { q.WorkDate, q.FinishedQty }).ToListAsync(ct);
        return batches.Where(q => others.Any(o => q.WorkDate >= o.StartDate && q.WorkDate <= o.EndDate)).Sum(q => q.FinishedQty);
    }

    /// <summary>QC đạt và hàng hủy (Scrap) của tổ theo dòng đơn, cả đơn (không theo kỳ): căn cứ chốt KPI.</summary>
    private async Task<(Dictionary<int, int> QcByLine, Dictionary<int, int> ScrapByLine)> LoadQcAndScrapAsync(
        KpiTarget target, List<int> lineIds, CancellationToken ct)
    {
        // QC đạt cả đơn của tổ (không theo kỳ): căn cứ chốt KPI.
        var qcByLine = await _uow.Repository<QcBatch>().Query().AsNoTracking()
            .Where(q => q.SewingTeamId == target.SewingTeamId && lineIds.Contains(q.OrderLineId))
            .GroupBy(q => q.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(q => q.FinishedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var scrapByLine = await _uow.Repository<DefectRecord>().Query().AsNoTracking()
            .Where(d => d.SewingTeamId == target.SewingTeamId && lineIds.Contains(d.OrderLineId)
                        && d.DefectType == DefectTypes.Scrap && d.Status != DefectStatuses.Cancelled)
            .GroupBy(d => d.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(d => d.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        return (qcByLine, scrapByLine);
    }

    /// <summary>
    /// Các công đoạn có lệch. Với mỗi (công đoạn, dòng đơn): lệch = sản lượng tổ báo − QC đạt − hàng hủy; &gt; 0 thì dòng đó lệch và
    /// kèm những công nhân đã báo công đoạn đó (sản lượng giảm dần). Hệ thống không biết ai báo dư nên không chia lệch cho từng người.
    /// Công đoạn xếp theo tổng lệch giảm dần, dòng đơn trong công đoạn cũng vậy.
    /// </summary>
    private async Task<List<EndOfOrderOperationDto>> BuildMismatchAsync(KpiTarget target, List<OrderLine> lines,
        Dictionary<int, int> qcByLine, Dictionary<int, int> scrapByLine, CancellationToken ct)
    {
        var lineById = lines.ToDictionary(l => l.OrderLineId);
        var reported = await _uow.Repository<OutputRecord>().Query().AsNoTracking()
            .Where(o => o.SewingTeamId == target.SewingTeamId && lineById.Keys.Contains(o.OrderLineId))
            .GroupBy(o => new { o.OperationId, o.OrderLineId, o.WorkerId })
            .Select(g => new { g.Key.OperationId, g.Key.OrderLineId, g.Key.WorkerId, Qty = g.Sum(o => o.Quantity) })
            .ToListAsync(ct);
        reported = reported.Where(r => r.Qty > 0).ToList();
        if (reported.Count == 0) return [];

        var operationIds = reported.Select(r => r.OperationId).Distinct().ToList();
        var operations = await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => operationIds.Contains(o.OperationId)).ToDictionaryAsync(o => o.OperationId, ct);
        var workerIds = reported.Select(r => r.WorkerId).Distinct().ToList();
        var workers = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => workerIds.Contains(e.EmployeeId))
            .Select(e => new { e.EmployeeId, e.EmployeeCode, e.FullName }).ToDictionaryAsync(e => e.EmployeeId, ct);

        var result = new List<EndOfOrderOperationDto>();
        foreach (var byOperation in reported.GroupBy(r => r.OperationId))
        {
            var mismatchLines = new List<EndOfOrderLineDto>();
            foreach (var byLine in byOperation.GroupBy(r => r.OrderLineId))
            {
                var total = byLine.Sum(r => r.Qty);
                var qc = qcByLine.GetValueOrDefault(byLine.Key);
                var scrap = scrapByLine.GetValueOrDefault(byLine.Key);
                var diff = total - qc - scrap;
                if (diff <= 0) continue;

                var line = lineById[byLine.Key];
                var people = byLine.OrderByDescending(r => r.Qty).ThenBy(r => workers[r.WorkerId].EmployeeCode)
                    .Select(r => new EndOfOrderWorkerDto(r.WorkerId, workers[r.WorkerId].EmployeeCode, workers[r.WorkerId].FullName, r.Qty))
                    .ToList();
                mismatchLines.Add(new EndOfOrderLineDto(line.OrderLineId, line.ProductVariant.ProductColor.Color,
                    line.ProductVariant.SizeCode, total, qc, scrap, diff, people));
            }
            if (mismatchLines.Count == 0) continue;

            var op = operations[byOperation.Key];
            var sorted = mismatchLines.OrderByDescending(l => l.MismatchQty).ThenBy(l => l.Color).ThenBy(l => l.SizeCode).ToList();
            result.Add(new EndOfOrderOperationDto(op.OperationId, op.OperationCode, op.OperationName, sorted.Sum(l => l.MismatchQty), sorted));
        }
        return result.OrderByDescending(o => o.MismatchQty).ThenBy(o => o.OperationCode).ToList();
    }

    // ---------- Thông báo ----------

    /// <summary>
    /// Có công đoạn lệch: báo Quản đốc và tổ trưởng của tổ bản TÓM TẮT có độ dài bị chặn — tối đa <see cref="MaxOperationsInNote"/> công
    /// đoạn lệch nhiều nhất, mỗi công đoạn tối đa <see cref="MaxWorkersInNote"/> người báo nhiều nhất kèm sản lượng; phần còn lại
    /// xem ở cửa sổ chi tiết mở NGAY TẠI CHỖ khi bấm thông báo (không chuyển trang): thông báo mang RefTable / RefId thay cho link.
    /// </summary>
    private async Task NotifyMismatchAsync(KpiTarget target, EndOfOrderPreviewDto preview, CancellationToken ct)
    {
        var message = MismatchSummary(preview);
        var title = Messages.NotifyEndOfOrderMismatchTitle;
        var note = new NotificationMessage(NotificationSources.Production, title, message, null,
            NotificationRefs.KpiMismatch, target.KpiTargetId);
        await _notifier.NotifyRoleAsync(RoleCodes.Manager, note, ct);
        await _notifier.NotifyTeamLeadersAsync(target.SewingTeamId, note, ct);
    }

    private static string MismatchSummary(EndOfOrderPreviewDto preview)
    {
        var shown = preview.Operations.Take(MaxOperationsInNote).Select(op =>
        {
            var byWorker = op.Lines.SelectMany(l => l.Workers).GroupBy(w => w.WorkerId)
                .Select(g => new { g.First().WorkerCode, g.First().WorkerName, Qty = g.Sum(w => w.ReportedQty) })
                .OrderByDescending(w => w.Qty).ThenBy(w => w.WorkerCode).ToList();
            var names = string.Join(", ", byWorker.Take(MaxWorkersInNote).Select(w => $"{w.WorkerCode} {w.WorkerName} {w.Qty}"));
            var more = byWorker.Count - MaxWorkersInNote;
            var moreText = more > 0 ? $" và {more} người khác" : string.Empty;
            return $"• {op.OperationName}: lệch {op.MismatchQty} ({op.Lines.Count} dòng đơn) — {names}{moreText}";
        });
        var rest = preview.Operations.Count - MaxOperationsInNote;
        var restText = rest > 0 ? $"\nvà {rest} công đoạn khác." : string.Empty;

        var message = $"Đơn {preview.OrderCode} · {preview.ProductCode} · {preview.TeamName}: sản lượng báo nhiều hơn QC đạt + hàng hủy " +
                      $"ở {preview.Operations.Count} công đoạn.\n{string.Join("\n", shown)}{restText}\n" +
                      "Bấm để xem chi tiết. Hệ thống không tự trừ lương, hãy xác minh rồi ghi phiếu lỗi cho đúng người nếu cần.";
        return message.Length > MaxNotificationLength ? message[..(MaxNotificationLength - 1)] + "…" : message;
    }

    /// <summary>KPI không đạt: báo Quản đốc và tổ trưởng của tổ (không còn đơn tăng ca trong DB).</summary>
    private async Task NotifyShortfallAsync(KpiTarget target, EndOfOrderPreviewDto preview, CancellationToken ct)
    {
        var small = preview.OtDecision == OvertimeDecisions.AutoApproved;
        var message = $"KPI đơn {preview.OrderCode} mã hàng {preview.ProductCode} của {preview.TeamName} chưa đạt: {preview.ActualQty}/{preview.TargetQty} " +
                      $"(thiếu {preview.ShortfallQty}, {preview.ShortfallPct:0.##}%). " +
                      (small ? $"Thiếu không quá {preview.ThresholdPct}%: nên tăng ca để bù." : "Thiếu nhiều: tổ trưởng quyết định có tăng ca hay không.");
        const string title = "KPI không đạt khi kết thúc đơn";
        await _notifier.NotifyRoleAsync(RoleCodes.Manager,
            new NotificationMessage(NotificationSources.Production, title, message, ManagerKpiLink), ct);
        await _notifier.NotifyTeamLeadersAsync(target.SewingTeamId,
            new NotificationMessage(NotificationSources.Production, title, message, TeamLeaderKpiLink), ct);
    }
}
