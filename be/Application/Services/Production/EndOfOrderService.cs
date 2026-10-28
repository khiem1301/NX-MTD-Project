using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services.Production;

/// <summary>
/// Kết thúc đơn của một tổ may (chỉ đánh giá khi đến cuối đơn, không theo ngày / tháng):
/// 1) so sản lượng công nhân báo ở công đoạn cuối với số QC đạt + hàng hủy; phần lệch ghi thẳng thành phiếu hàng hỏng
///    (lý do "Lệch kiểm cuối đơn") gắn với từng công nhân theo tỉ lệ sản lượng đã báo;
/// 2) chốt KPI (đạt / không đạt theo số QC đạt cả đơn) và đóng chỉ tiêu;
/// 3) KPI không đạt: thiếu không quá ngưỡng thì tự duyệt tăng ca, thiếu nhiều hơn thì tổ trưởng quyết định.
/// </summary>
public sealed class EndOfOrderService : IEndOfOrderService
{
    public const int DefaultThresholdPct = 10;

    private readonly IUnitOfWork _uow;
    private readonly ProductionGuard _guard;
    private readonly IPieceRateService _rates;
    private readonly IRequestService _requests;
    private readonly INotificationService _notifier;
    private readonly IAuditLogService _audit;
    private readonly ISystemSettingReader _settings;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public EndOfOrderService(IUnitOfWork uow, ProductionGuard guard, IPieceRateService rates, IRequestService requests,
        INotificationService notifier, IAuditLogService audit, ISystemSettingReader settings, ICurrentUser user, IClock clock)
    {
        _uow = uow; _guard = guard; _rates = rates; _requests = requests; _notifier = notifier; _audit = audit;
        _settings = settings; _user = user; _clock = clock;
    }

    public async Task<Result<EndOfOrderPreviewDto>> PreviewAsync(int kpiTargetId, CancellationToken ct)
    {
        var target = await FindTargetAsync(kpiTargetId, ct);
        if (target is null) return Fail.NotFound<EndOfOrderPreviewDto>();
        return Result<EndOfOrderPreviewDto>.Success((await BuildAsync(target, ct)).Preview);
    }

    public async Task<Result<EndOfOrderPreviewDto>> FinishAsync(int kpiTargetId, CancellationToken ct)
    {
        var target = await _uow.Repository<KpiTarget>().Query().Include(k => k.Order).Include(k => k.Product).Include(k => k.SewingTeam)
            .FirstOrDefaultAsync(k => k.KpiTargetId == kpiTargetId, ct);
        if (target is null) return Fail.NotFound<EndOfOrderPreviewDto>();
        if (target.Status != KpiStatuses.Active)
            return Result<EndOfOrderPreviewDto>.Failure(ErrorCodes.KpiInvalidStatus, Messages.KpiInvalidStatusForFinish);

        var built = await BuildAsync(target, ct);
        if (built.Preview.Blockers.Count > 0)
            return Result<EndOfOrderPreviewDto>.Failure(ErrorCodes.KpiFinishBlocked, string.Join(" ", built.Preview.Blockers));

        var today = _clock.Today;
        var now = _clock.Now;
        var preview = built.Preview;

        var result = await _uow.InTransactionAsync(async () =>
        {
            var created = new List<DefectRecord>();
            if (built.FinalOperation is { } finalOp && built.RateId is { } rateId)
            {
                var seq = await NextSequenceAsync(target.SewingTeam.TeamCode, today, ct);
                foreach (var row in preview.Rows.Where(r => r.MismatchQty > 0))
                {
                    var defect = new DefectRecord
                    {
                        DefectCode = $"{target.SewingTeam.TeamCode}-{today:yyMMdd}-{seq++:000}", WorkDate = today, ShiftCode = null,
                        SewingTeamId = target.SewingTeamId, OrderLineId = row.OrderLineId, OperationId = finalOp.OperationId,
                        ResponsibleWorkerId = row.WorkerId, DefectType = DefectTypes.Scrap, Quantity = row.MismatchQty,
                        DefectReasonCode = DefectReasons.EndOfOrderMismatch,
                        ReasonNote = $"Lệch kiểm cuối đơn {target.Order.OrderCode}: sản lượng báo ở công đoạn cuối nhiều hơn số QC đạt và hàng hủy.",
                        PieceRateId = rateId, WorkerNotifiedAt = now, EditDeadline = ProductionGuard.NextWorkingDayEnd(today),
                        Status = DefectStatuses.Recorded, RecordedBy = _user.EmployeeId, RecordedAt = now,
                    };
                    await _uow.Repository<DefectRecord>().AddAsync(defect);
                    created.Add(defect);
                }
                if (created.Count > 0) await _uow.SaveChangesAsync();   // lấy DefectRecordId cho nhật ký / thông báo
            }
            foreach (var d in created)
            {
                _audit.Add(new AuditEntry(nameof(DefectRecord), d.DefectRecordId, AuditActions.Insert, null,
                    new { d.DefectCode, d.ResponsibleWorkerId, d.Quantity, d.DefectReasonCode }, "Kiểm cuối đơn"));
                _notifier.NotifyEmployees([d.ResponsibleWorkerId], new NotificationMessage(NotificationSources.Production,
                    Messages.NotifyDefectTitle,
                    $"Kiểm cuối đơn {target.Order.OrderCode}: {d.Quantity} sản phẩm báo ở công đoạn cuối chưa có QC đạt, ghi hàng hủy (trừ lương).",
                    null, nameof(DefectRecord), d.DefectRecordId));
            }

            var before = new { target.Status, target.Result };
            target.Result = preview.Achieved ? KpiResults.Achieved : KpiResults.NotAchieved;
            target.Status = KpiStatuses.Closed;
            _audit.Add(new AuditEntry(nameof(KpiTarget), target.KpiTargetId, AuditActions.Update, before,
                new { target.Status, target.Result, ActualQty = preview.ActualQty, MismatchRecords = created.Count }, "Kết thúc đơn của tổ"));

            if (preview.OtDecision != OvertimeDecisions.None)
                await StageOvertimeAsync(target, preview, ct);

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

    private sealed record Built(EndOfOrderPreviewDto Preview, Operation? FinalOperation, int? RateId);

    private async Task<Built> BuildAsync(KpiTarget target, CancellationToken ct)
    {
        var blockers = new List<string>();
        var threshold = await _settings.GetIntAsync(SettingKeys.KpiOtAutoThresholdPct, DefaultThresholdPct, ct);
        var today = _clock.Today;

        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking().Include(l => l.ProductVariant)
            .Where(l => l.OrderId == target.OrderId && l.ProductId == target.ProductId).ToListAsync(ct);
        var lineIds = lines.Select(l => l.OrderLineId).ToList();

        // QC đạt cả đơn của tổ (không theo kỳ): căn cứ chốt KPI.
        var qcByLine = await _uow.Repository<QcBatch>().Query().AsNoTracking()
            .Where(q => q.SewingTeamId == target.SewingTeamId && lineIds.Contains(q.OrderLineId))
            .GroupBy(q => q.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(q => q.FinishedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var actual = qcByLine.Values.Sum();

        var scrapByLine = await _uow.Repository<DefectRecord>().Query().AsNoTracking()
            .Where(d => d.SewingTeamId == target.SewingTeamId && lineIds.Contains(d.OrderLineId)
                        && d.DefectType == DefectTypes.Scrap && d.Status != DefectStatuses.Cancelled)
            .GroupBy(d => d.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(d => d.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var finalOp = await _uow.Repository<Operation>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.ProductId == target.ProductId && o.IsFinalStep && o.Status == OperationStatuses.Active, ct);
        var rows = new List<EndOfOrderRowDto>();
        int? rateId = null;
        if (finalOp is null)
            blockers.Add(Messages.KpiFinishNoFinalOperation);
        else
        {
            var reported = await _uow.Repository<OutputRecord>().Query().AsNoTracking()
                .Where(o => o.SewingTeamId == target.SewingTeamId && o.OperationId == finalOp.OperationId && lineIds.Contains(o.OrderLineId))
                .GroupBy(o => new { o.OrderLineId, o.WorkerId })
                .Select(g => new { g.Key.OrderLineId, g.Key.WorkerId, Qty = g.Sum(o => o.Quantity) })
                .ToListAsync(ct);
            var workers = await _uow.Repository<Employee>().Query().AsNoTracking()
                .Where(e => reported.Select(r => r.WorkerId).Distinct().Contains(e.EmployeeId))
                .Select(e => new { e.EmployeeId, e.EmployeeCode, e.FullName }).ToListAsync(ct);

            foreach (var line in lines.OrderBy(l => l.ProductVariant.Color).ThenBy(l => l.ProductVariant.SizeCode))
            {
                var perWorker = reported.Where(r => r.OrderLineId == line.OrderLineId && r.Qty > 0).OrderBy(r => r.WorkerId).ToList();
                var totalReported = perWorker.Sum(r => r.Qty);
                var diff = Math.Max(0, totalReported - qcByLine.GetValueOrDefault(line.OrderLineId) - scrapByLine.GetValueOrDefault(line.OrderLineId));
                foreach (var (r, qty) in perWorker.Zip(Distribute(diff, perWorker.Select(r => r.Qty).ToList())))
                {
                    var w = workers.First(x => x.EmployeeId == r.WorkerId);
                    rows.Add(new EndOfOrderRowDto(line.OrderLineId, line.ProductVariant.Color, line.ProductVariant.SizeCode,
                        r.WorkerId, w.EmployeeCode, w.FullName, r.Qty, qty));
                }
            }

            if (rows.Any(r => r.MismatchQty > 0))
            {
                var rate = await _rates.FindEffectiveAsync(finalOp.OperationId, today, ct);
                if (rate is null)
                    blockers.Add(string.Format(Messages.PieceRateNotApproved, finalOp.OperationCode, today.ToString("dd/MM/yyyy")));
                else rateId = rate.PieceRateId;
                if (await _guard.IsPeriodLockedAsync(today, ct)) blockers.Add(_guard.PeriodLockedFailure(today).Message ?? string.Empty);
            }
        }

        var achieved = actual >= target.TargetFinishedQty;
        var shortfall = Math.Max(0, target.TargetFinishedQty - actual);
        var pct = target.TargetFinishedQty > 0 ? Math.Round(100m * shortfall / target.TargetFinishedQty, 2) : 0m;
        var ot = achieved ? OvertimeDecisions.None : pct <= threshold ? OvertimeDecisions.AutoApproved : OvertimeDecisions.LeaderDecides;

        var preview = new EndOfOrderPreviewDto(target.KpiTargetId, target.Order.OrderCode, target.Product.ProductCode,
            target.SewingTeam.TeamName, target.TargetFinishedQty, actual, achieved, shortfall, pct, threshold, ot,
            finalOp?.OperationName, rows, blockers, target.Status == KpiStatuses.Active);
        return new Built(preview, finalOp, rateId);
    }

    /// <summary>Chia <paramref name="total"/> theo tỉ lệ <paramref name="weights"/> thành số nguyên (phần dư chia cho phần lẻ lớn nhất).</summary>
    public static IReadOnlyList<int> Distribute(int total, IReadOnlyList<int> weights)
    {
        var result = new int[weights.Count];
        var sum = weights.Sum();
        if (total <= 0 || sum <= 0) return result;
        var remainders = new (int Index, long Rem)[weights.Count];
        var given = 0;
        for (var i = 0; i < weights.Count; i++)
        {
            var scaled = (long)total * weights[i];
            result[i] = (int)(scaled / sum);
            given += result[i];
            remainders[i] = (i, scaled % sum);
        }
        foreach (var (index, _) in remainders.OrderByDescending(r => r.Rem).ThenBy(r => r.Index).Take(total - given))
            result[index]++;
        return result;
    }

    private async Task<int> NextSequenceAsync(string teamCode, DateOnly date, CancellationToken ct)
    {
        var prefix = $"{teamCode}-{date:yyMMdd}-";
        var codes = await _uow.Repository<DefectRecord>().Query().AsNoTracking().Where(d => d.DefectCode.StartsWith(prefix))
            .Select(d => d.DefectCode).ToListAsync(ct);
        return codes.Select(c => int.TryParse(c[prefix.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
    }

    private async Task StageOvertimeAsync(KpiTarget target, EndOfOrderPreviewDto preview, CancellationToken ct)
    {
        var auto = preview.OtDecision == OvertimeDecisions.AutoApproved;
        var content = $"KPI đơn {preview.OrderCode} mã hàng {preview.ProductCode} chưa đạt: {preview.ActualQty}/{preview.TargetQty} " +
                      $"(thiếu {preview.ShortfallQty}, {preview.ShortfallPct:0.##}%). " +
                      (auto ? $"Thiếu không quá {preview.ThresholdPct}% nên tự duyệt tăng ca." : "Tổ trưởng quyết định có tăng ca hay không.");
        var draft = new RequestDraft(RequestTypes.Overtime, target.OrderId, null, target.KpiTargetId, null, null, null, null,
            $"Tăng ca KPI {preview.OrderCode}", content, Array.Empty<RequestLineInput>(), null);
        await _requests.StageSystemRequestAsync(draft, ct, target.SewingTeamId,
            auto ? $"Tự duyệt: thiếu {preview.ShortfallPct:0.##}% ≤ {preview.ThresholdPct}%." : null);
    }
}
