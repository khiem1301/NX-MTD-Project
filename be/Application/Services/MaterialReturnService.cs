using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// UC42 (TH-10): CHỈ tổ cắt trả vải thừa, theo phiếu cấp gốc (đã nhận); số trả ≤ số đã nhận − số đã trả (kể cả đang chờ).
/// Thủ kho đếm, xác nhận (cộng kho NVL, lệch phải có lý do) hoặc từ chối có lý do. Tổ may không trả phụ liệu.
/// </summary>
public sealed class MaterialReturnService : IMaterialReturnService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly IDocumentCodeGenerator _codes;
    private readonly INotificationService _notifier;
    private readonly IAppLock _locks;
    private readonly IClock _clock;

    public MaterialReturnService(IUnitOfWork uow, IStockService stock, IDocumentCodeGenerator codes,
        INotificationService notifier, IAppLock locks, IClock clock)
    {
        _uow = uow; _stock = stock; _codes = codes; _notifier = notifier; _locks = locks; _clock = clock;
    }

    private IQueryable<MaterialReturn> Returns => _uow.Repository<MaterialReturn>().Query();

    public async Task<Result<PagedResult<MaterialReturnListItemDto>>> GetPagedAsync(MaterialReturnQuery query,
        int? cuttingTeamId, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Returns.AsNoTracking()
            .Where(r => cuttingTeamId == null || r.MaterialIssue.ReceivingTeamId == cuttingTeamId)
            .Where(r => string.IsNullOrEmpty(query.Status) || r.Status == query.Status)
            .Where(r => string.IsNullOrEmpty(keyword) || r.ReturnCode.Contains(keyword)
                        || r.MaterialIssue.IssueCode.Contains(keyword) || r.MaterialIssue.Order.OrderCode.Contains(keyword))
            .OrderByDescending(r => r.MaterialReturnId)
            .Select(r => new MaterialReturnListItemDto(r.MaterialReturnId, r.ReturnCode, r.MaterialIssueId,
                r.MaterialIssue.IssueCode, r.MaterialIssue.Order.OrderCode, r.MaterialIssue.ReceivingTeamId, r.MaterialIssue.ReceivingTeam.TeamName,
                r.Status, r.CreatedAt, r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName,
                r.ConfirmedAt, r.MaterialReturnLines.Sum(l => l.ReturnedQty)))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<MaterialReturnListItemDto>>.Success(page);
    }

    public async Task<Result<MaterialReturnDto>> GetByIdAsync(int returnId, int? cuttingTeamId, CancellationToken ct)
    {
        var dto = await Returns.AsNoTracking().Where(r => r.MaterialReturnId == returnId)
            .Select(r => new MaterialReturnDto(r.MaterialReturnId, r.ReturnCode, r.MaterialIssueId, r.MaterialIssue.IssueCode,
                r.MaterialIssue.Order.OrderCode, r.MaterialIssue.ReceivingTeamId, r.MaterialIssue.ReceivingTeam.TeamName, r.Note, r.Status, r.CreatedAt,
                r.ConfirmedByNavigation == null ? null : r.ConfirmedByNavigation.FullName, r.ConfirmedAt, r.RejectReason,
                r.MaterialReturnLines.OrderBy(l => l.MaterialReturnLineId).Select(l => new MaterialReturnLineDto(
                    l.MaterialReturnLineId, l.MaterialIssueLineId, l.MaterialIssueLine.Material!.MaterialCode,
                    l.MaterialIssueLine.Material.MaterialName, l.MaterialIssueLine.Material.Unit,
                    l.MaterialIssueLine.Material.Color, l.ReturnedQty, l.ConfirmedQty, l.DiscrepancyReason)).ToList()))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<MaterialReturnDto>();
        return cuttingTeamId is not null && dto.CuttingTeamId != cuttingTeamId
            ? WarehouseFailures.TeamMismatch<MaterialReturnDto>()
            : Result<MaterialReturnDto>.Success(dto);
    }

    public async Task<Result<IReadOnlyList<ReturnableLineDto>>> GetReturnableAsync(int cuttingTeamId, CancellationToken ct)
    {
        var lines = await _uow.Repository<MaterialIssueLine>().Query().AsNoTracking()
            .Where(l => l.MaterialIssue.ReceivingTeamId == cuttingTeamId && l.MaterialIssue.WarehouseType == WarehouseTypes.Material
                        && l.MaterialIssue.Status == IssueStatuses.Received && l.MaterialId != null)
            .OrderByDescending(l => l.MaterialIssueId).ThenBy(l => l.MaterialIssueLineId)
            .Select(l => new
            {
                l.MaterialIssueId, l.MaterialIssue.IssueCode, l.MaterialIssueLineId, l.MaterialIssue.Order.OrderCode,
                l.Material!.MaterialCode, l.Material.MaterialName, l.Material.Unit, Color = l.Material.Color,
                Received = l.ConfirmedQty ?? 0,
            }).ToListAsync(ct);
        var returned = await ReturnedAsync(lines.Select(l => l.MaterialIssueLineId).ToList(), null, ct);
        var result = lines.Select(l =>
        {
            var done = returned.GetValueOrDefault(l.MaterialIssueLineId);
            return new ReturnableLineDto(l.MaterialIssueId, l.IssueCode, l.MaterialIssueLineId, l.OrderCode, l.MaterialCode,
                l.MaterialName, l.Unit, l.Color, l.Received, done, Math.Max(0, l.Received - done));
        }).Where(l => l.RemainingQty > 0).ToList();
        return Result<IReadOnlyList<ReturnableLineDto>>.Success(result);
    }

    public async Task<Result<MaterialReturnDto>> CreateAsync(CreateMaterialReturnDto dto, int cuttingTeamId, CancellationToken ct)
    {
        var created = await _uow.InTransactionAsync(async () =>
        {
            var team = await _uow.Repository<Team>().Query().AsNoTracking().FirstOrDefaultAsync(t => t.TeamId == cuttingTeamId, ct);
            if (team is null || team.TeamType != TeamTypes.Cutting) return WarehouseFailures.TeamMismatch<int>();

            var issue = await _uow.Repository<MaterialIssue>().Query().AsNoTracking().Include(i => i.MaterialIssueLines)
                .FirstOrDefaultAsync(i => i.MaterialIssueId == dto.MaterialIssueId, ct);
            if (issue is null) return WarehouseFailures.NotFound<int>();
            if (issue.ReceivingTeamId != cuttingTeamId) return WarehouseFailures.TeamMismatch<int>();
            if (issue.WarehouseType != WarehouseTypes.Material || issue.Status != IssueStatuses.Received)
                return Result<int>.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.ReturnIssueNotReturnable);

            await _locks.AcquireAsync($"material-return:{issue.MaterialIssueId}", ct);
            var byLine = issue.MaterialIssueLines.Where(l => l.MaterialId != null).ToDictionary(l => l.MaterialIssueLineId);
            if (dto.Lines.Any(l => !byLine.ContainsKey(l.MaterialIssueLineId)))
                return Result<int>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ReturnLineNotInIssue);
            var returned = await ReturnedAsync(byLine.Keys.ToList(), null, ct);
            var over = dto.Lines
                .Select(l => (Line: l, Remaining: (byLine[l.MaterialIssueLineId].ConfirmedQty ?? 0) - returned.GetValueOrDefault(l.MaterialIssueLineId)))
                .Where(x => x.Line.ReturnedQty > x.Remaining)
                .Select(x => $"còn trả được {x.Remaining:0.###}, trả {x.Line.ReturnedQty:0.###}").ToList();
            if (over.Count > 0)
                return Result<int>.Failure(ErrorCodes.ReturnExceedsIssued, $"{WarehouseMessages.ReturnExceedsIssued}: {string.Join("; ", over)}.");

            var ret = new MaterialReturn
            {
                ReturnCode = await _codes.NextAsync<MaterialReturn>(DocumentPrefixes.MaterialReturn, r => r.ReturnCode, ct),
                MaterialIssueId = issue.MaterialIssueId,
                Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                Status = HandoverStatuses.Pending,
                CreatedAt = _clock.Now,
                MaterialReturnLines = dto.Lines.Select(l => new MaterialReturnLine
                    { MaterialIssueLineId = l.MaterialIssueLineId, ReturnedQty = l.ReturnedQty }).ToList(),
            };
            await _uow.Repository<MaterialReturn>().AddAsync(ret);
            await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
                "Chờ nhận vải thừa", $"Tổ cắt {team.TeamName} trả vải theo phiếu {issue.IssueCode}, vui lòng đếm và nhận vào kho.", null), ct);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(ret.MaterialReturnId, WarehouseMessages.ReturnCreated);
        });
        return created.IsSuccess ? await GetByIdAsync(created.Data, cuttingTeamId, ct) : WarehouseFailures.From<MaterialReturnDto>(created);
    }

    public Task<Result> ConfirmAsync(int returnId, ConfirmMaterialReturnDto dto, int employeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var ret = await Returns.Include(r => r.MaterialReturnLines).ThenInclude(l => l.MaterialIssueLine)
                .FirstOrDefaultAsync(r => r.MaterialReturnId == returnId, ct);
            if (ret is null) return WarehouseFailures.NotFound();
            if (ret.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

            var byId = dto.Lines.ToDictionary(l => l.MaterialReturnLineId);
            if (byId.Count != ret.MaterialReturnLines.Count || ret.MaterialReturnLines.Any(l => !byId.ContainsKey(l.MaterialReturnLineId)))
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LinesRequired);
            foreach (var line in ret.MaterialReturnLines)
            {
                var input = byId[line.MaterialReturnLineId];
                if (input.ConfirmedQty > line.ReturnedQty)
                    return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.HandoverConfirmedExceedsHanded);
                if (input.ConfirmedQty != line.ReturnedQty && string.IsNullOrWhiteSpace(input.DiscrepancyReason))
                    return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DiscrepancyReasonRequired);
                line.ConfirmedQty = input.ConfirmedQty;
                line.DiscrepancyReason = input.ConfirmedQty == line.ReturnedQty ? null : input.DiscrepancyReason!.Trim();
            }

            var today = _clock.Today;
            var movements = ret.MaterialReturnLines.Where(l => l.ConfirmedQty > 0)
                .Select(l => new StockMovement(StockKey.Material(l.MaterialIssueLine.MaterialId!.Value),
                    l.ConfirmedQty!.Value, StockDocTypes.MaterialReturn, ret.MaterialReturnId, l.MaterialReturnLineId, today)).ToList();
            if (movements.Count > 0)
            {
                var posted = await _stock.PostAsync(movements, ct);
                if (!posted.IsSuccess) return posted;
            }

            ret.Status = HandoverStatuses.Confirmed;
            ret.ConfirmedBy = employeeId;
            ret.ConfirmedAt = _clock.Now;
            await _uow.SaveChangesAsync();
            return Result.Success(WarehouseMessages.ReturnConfirmed);
        });

    public async Task<Result> RejectAsync(int returnId, ReasonDto dto, int employeeId, CancellationToken ct)
    {
        var ret = await Returns.FirstOrDefaultAsync(r => r.MaterialReturnId == returnId, ct);
        if (ret is null) return WarehouseFailures.NotFound();
        if (ret.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

        ret.Status = HandoverStatuses.Rejected;
        ret.RejectReason = dto.Reason.Trim();
        ret.ConfirmedBy = employeeId;
        ret.ConfirmedAt = _clock.Now;
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.ReturnRejected);
    }

    public async Task<Result> CancelAsync(int returnId, int cuttingTeamId, CancellationToken ct)
    {
        var ret = await Returns.FirstOrDefaultAsync(r => r.MaterialReturnId == returnId && r.MaterialIssue.ReceivingTeamId == cuttingTeamId, ct);
        if (ret is null) return WarehouseFailures.NotFound();
        if (ret.Status != HandoverStatuses.Pending) return WarehouseFailures.InvalidStatus();

        ret.Status = HandoverStatuses.Cancelled;
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.ReturnCancelled);
    }

    /// <summary>Số đã trả của từng dòng phiếu cấp: phiếu chờ tính số tổ trả, phiếu đã nhận tính số kho đếm.</summary>
    private async Task<Dictionary<int, decimal>> ReturnedAsync(IReadOnlyCollection<int> issueLineIds, int? excludeReturnId,
        CancellationToken ct)
    {
        var rows = await _uow.Repository<MaterialReturnLine>().Query().AsNoTracking()
            .Where(l => issueLineIds.Contains(l.MaterialIssueLineId) && l.MaterialReturnId != excludeReturnId
                        && (l.MaterialReturn.Status == HandoverStatuses.Pending || l.MaterialReturn.Status == HandoverStatuses.Confirmed))
            .Select(l => new { l.MaterialIssueLineId, Qty = l.MaterialReturn.Status == HandoverStatuses.Confirmed ? l.ConfirmedQty ?? 0 : l.ReturnedQty })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.MaterialIssueLineId).ToDictionary(g => g.Key, g => g.Sum(r => r.Qty));
    }
}
