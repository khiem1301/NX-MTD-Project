using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed class MaterialIssueService : IMaterialIssueService
{
    private sealed record IssueTarget(int OrderId, int TeamId, int? WorkOrderId);

    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly FabricPlanner _planner;
    private readonly IDocumentCodeGenerator _codes;
    private readonly IClock _clock;
    private readonly ILogger<MaterialIssueService> _logger;
    private readonly IRequestService _requests;
    private readonly IAuditLogService _audit;
    private readonly INotificationService _notifier;

    public MaterialIssueService(IUnitOfWork uow, IStockService stock, FabricPlanner planner,
        IDocumentCodeGenerator codes, IClock clock, ILogger<MaterialIssueService> logger, IRequestService requests,
        IAuditLogService audit, INotificationService notifier)
    {
        _uow = uow; _stock = stock; _planner = planner; _codes = codes; _clock = clock; _logger = logger; _requests = requests;
        _audit = audit; _notifier = notifier;
    }

    private const string IssueLink = "/warehouse/material-issues";

    private IQueryable<MaterialIssue> Issues => _uow.Repository<MaterialIssue>().Query();

    public async Task<Result<PagedResult<MaterialIssueListItemDto>>> GetPagedAsync(MaterialIssueQuery query,
        int? receivingTeamId, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await Issues.AsNoTracking()
            .Where(i => receivingTeamId == null || i.ReceivingTeamId == receivingTeamId)
            .Where(i => string.IsNullOrEmpty(query.Status) || i.Status == query.Status)
            .Where(i => string.IsNullOrEmpty(query.WarehouseType) || i.WarehouseType == query.WarehouseType)
            .Where(i => query.OrderId == null || i.OrderId == query.OrderId)
            .Where(i => query.WorkOrderId == null || i.WorkOrderId == query.WorkOrderId)
            .Where(i => string.IsNullOrEmpty(keyword) || i.IssueCode.Contains(keyword) || i.Order.OrderCode.Contains(keyword))
            .OrderByDescending(i => i.MaterialIssueId)
            .Select(i => new MaterialIssueListItemDto(i.MaterialIssueId, i.IssueCode, i.WarehouseType, i.OrderId,
                i.Order.OrderCode, i.WorkOrderId, i.WorkOrder == null ? null : i.WorkOrder.WorkOrderCode, i.RequestId,
                i.ReceivingTeamId, i.ReceivingTeam.TeamName, i.IssueDate, i.Status, i.IssuedByNavigation.FullName,
                i.ReceivedAt, i.MaterialIssueLines.Count))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<MaterialIssueListItemDto>>.Success(page);
    }

    public async Task<Result<MaterialIssueDto>> GetByIdAsync(int issueId, int? receivingTeamId, CancellationToken ct)
    {
        var dto = await Issues.AsNoTracking().Where(i => i.MaterialIssueId == issueId)
            .Select(i => new MaterialIssueDto(i.MaterialIssueId, i.IssueCode, i.WarehouseType, i.OrderId,
                i.Order.OrderCode, i.WorkOrderId, i.WorkOrder == null ? null : i.WorkOrder.WorkOrderCode, i.RequestId,
                i.ReceivingTeamId, i.ReceivingTeam.TeamName, i.IssueDate, i.Status, i.IssuedByNavigation.FullName,
                i.ReceivedByNavigation == null ? null : i.ReceivedByNavigation.FullName, i.ReceivedAt, i.RejectReason,
                i.CreatedAt,
                i.MaterialIssueLines.OrderBy(l => l.MaterialIssueLineId).Select(l => new MaterialIssueLineDto(
                    l.MaterialIssueLineId, l.MaterialId, l.Material == null ? null : l.Material.MaterialCode,
                    l.Material == null ? null : l.Material.MaterialName, l.Material == null ? null : l.Material.Unit,
                    l.Material == null ? null : l.Material.Color, l.OrderLineId, l.ProductComponentMaterialId,
                    l.ProductComponentMaterial == null ? null : l.ProductComponentMaterial.ProductComponent.ComponentName + " – " + l.ProductComponentMaterial.Material.MaterialName + (l.ProductComponentMaterial.Material.Color == "" ? "" : " " + l.ProductComponentMaterial.Material.Color),
                    l.OrderLine == null ? null : l.OrderLine.ProductVariant.Product.ProductCode,
                    l.OrderLine == null ? null : l.OrderLine.ProductVariant.Color, l.OrderLine == null ? null : l.OrderLine.ProductVariant.SizeCode,
                    l.IssuedQty, l.ConfirmedQty, l.DiscrepancyReason)).ToList()))
            .FirstOrDefaultAsync(ct);
        if (dto is null) return WarehouseFailures.NotFound<MaterialIssueDto>();
        if (receivingTeamId is not null && dto.ReceivingTeamId != receivingTeamId)
            return WarehouseFailures.TeamMismatch<MaterialIssueDto>();
        return Result<MaterialIssueDto>.Success(dto);
    }

    public async Task<Result<MaterialIssueDto>> CreateAsync(CreateMaterialIssueDto dto, int issuedByEmployeeId,
        CancellationToken ct)
    {
        var (error, target) = await ResolveTargetAsync(dto, ct);
        if (error is not null) return error;

        var created = await _uow.InTransactionAsync(async () =>
        {
            var lines = dto.Lines.Select(l => new MaterialIssueLine
            {
                MaterialId = l.MaterialId,
                OrderLineId = l.OrderLineId,
                ProductComponentMaterialId = l.OrderLineId is null ? null : l.ProductComponentMaterialId,
                IssuedQty = l.IssuedQty,
            }).ToList();
            var available = await _stock.EnsureAvailableAsync(lines.Select(l => new StockRequirement(KeyOf(dto.WarehouseType, l), l.IssuedQty)).ToList(), ct);
            if (!available.IsSuccess) return WarehouseFailures.From<int>(available);

            var issue = new MaterialIssue
            {
                IssueCode = await _codes.NextAsync<MaterialIssue>(DocumentPrefixes.MaterialIssue, i => i.IssueCode, ct),
                WarehouseType = dto.WarehouseType,
                RequestId = dto.RequestId,
                OrderId = target!.OrderId,
                WorkOrderId = target.WorkOrderId,
                ReceivingTeamId = target.TeamId,
                IssueDate = dto.IssueDate,
                IssuedBy = issuedByEmployeeId,
                Status = IssueStatuses.PendingReceipt,
                MaterialIssueLines = lines,
            };
            await _uow.Repository<MaterialIssue>().AddAsync(issue);
            // Thủ kho lập phiếu cấp = đã xử lý đơn xin (DEV-TH-05-1): đơn xin sang Hoàn thành cùng giao dịch.
            if (dto.RequestId is { } requestId)
            {
                var done = await _requests.MarkCompletedAsync(requestId, issue.IssueCode, ct);
                if (!done.IsSuccess) return WarehouseFailures.From<int>(done);
            }
            // Tổ trưởng tổ nhận là người xử lý tiếp: kiểm đếm và xác nhận nhận.
            await _notifier.NotifyTeamLeadersAsync(target.TeamId, new NotificationMessage(NotificationSources.Warehouse,
                dto.WarehouseType == WarehouseTypes.SemiFinished ? "Có phiếu cấp BTP chờ nhận" : "Có phiếu cấp vải chờ nhận",
                $"Thủ kho đã lập phiếu cấp {issue.IssueCode} cho tổ bạn, vui lòng kiểm đếm và xác nhận nhận.", IssueLink), ct);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(issue.MaterialIssueId);
        });
        if (!created.IsSuccess) return WarehouseFailures.From<MaterialIssueDto>(created);
        _logger.LogInformation("Material issue {IssueId} ({WarehouseType}) created for team {TeamId}",
            created.Data, dto.WarehouseType, target!.TeamId);
        return await GetByIdAsync(created.Data, null, ct);
    }

    public Task<Result> ConfirmAsync(int issueId, ConfirmIssueDto dto, int teamId, int employeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var issue = await Issues.Include(i => i.MaterialIssueLines).FirstOrDefaultAsync(i => i.MaterialIssueId == issueId, ct);
            var check = CheckPending(issue, teamId);
            if (check is not null) return check;

            var byId = dto.Lines.ToDictionary(l => l.MaterialIssueLineId);
            if (byId.Count != issue!.MaterialIssueLines.Count || issue.MaterialIssueLines.Any(l => !byId.ContainsKey(l.MaterialIssueLineId)))
                return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.LinesRequired);

            foreach (var line in issue.MaterialIssueLines)
            {
                var input = byId[line.MaterialIssueLineId];
                if (input.ConfirmedQty > line.IssuedQty)
                    return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ConfirmedQtyExceedsIssued);
                if (input.ConfirmedQty != line.IssuedQty && string.IsNullOrWhiteSpace(input.DiscrepancyReason))
                    return Result.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.DiscrepancyReasonRequired);
                line.ConfirmedQty = input.ConfirmedQty;
                line.DiscrepancyReason = input.ConfirmedQty == line.IssuedQty ? null : input.DiscrepancyReason!.Trim();
            }

            var today = _clock.Today;
            var movements = issue.MaterialIssueLines.Where(l => l.ConfirmedQty > 0)
                .Select(l => new StockMovement(KeyOf(issue.WarehouseType, l), -l.ConfirmedQty!.Value,
                    StockDocTypes.MaterialIssue, issue.MaterialIssueId, l.MaterialIssueLineId, today)).ToList();
            if (movements.Count > 0)
            {
                var posted = await _stock.PostAsync(movements, ct);
                if (!posted.IsSuccess) return posted;
            }

            issue.Status = IssueStatuses.Received;
            issue.ReceivedBy = employeeId;
            issue.ReceivedAt = _clock.Now;
            if (issue.WarehouseType == WarehouseTypes.SemiFinished) await StartProductionAsync(issue.OrderId, ct);
            await _uow.SaveChangesAsync();
            return Result.Success(WarehouseMessages.IssueReceived);
        });

    /// <summary>
    /// Tổ may xác nhận nhận BTP (swimlane III.3.4) thì đơn chuyển sang IN_PRODUCTION nếu còn NEW và NVL không còn chờ.
    /// Nút "Chuyển sang sản xuất" của Admin vẫn dùng được; bước nào đến trước thì đổi trạng thái.
    /// </summary>
    private async Task StartProductionAsync(int orderId, CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().FirstOrDefaultAsync(o => o.OrderId == orderId, ct);
        if (order is null || order.Status != OrderStatuses.New || order.MaterialStatus == OrderMaterialStatuses.Awaiting) return;
        order.Status = OrderStatuses.InProduction;
        order.UpdatedAt = _clock.Now;
        _audit.Add(new AuditEntry(nameof(CustomerOrder), order.OrderId, AuditActions.Update,
            new { Status = OrderStatuses.New }, new { order.Status }, "Tổ may đã nhận BTP"));
    }

    public async Task<Result> RejectAsync(int issueId, ReasonDto dto, int teamId, CancellationToken ct)
    {
        var issue = await Issues.FirstOrDefaultAsync(i => i.MaterialIssueId == issueId, ct);
        var check = CheckPending(issue, teamId);
        if (check is not null) return check;

        issue!.Status = IssueStatuses.Rejected;
        issue.RejectReason = dto.Reason.Trim();
        // Thủ kho lập phiếu phải xử lý lại.
        _notifier.NotifyEmployees([issue.IssuedBy], new NotificationMessage(NotificationSources.Warehouse, "Phiếu cấp bị tổ từ chối",
            $"Tổ nhận từ chối phiếu cấp {issue.IssueCode}: {issue.RejectReason}", IssueLink));
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.IssueRejected);
    }

    private static Result? CheckPending(MaterialIssue? issue, int teamId)
    {
        if (issue is null) return WarehouseFailures.NotFound();
        if (issue.ReceivingTeamId != teamId) return WarehouseFailures.TeamMismatch();
        return issue.Status == IssueStatuses.PendingReceipt ? null : WarehouseFailures.InvalidStatus();
    }

    private async Task<(Result<MaterialIssueDto>? Error, IssueTarget? Target)> ResolveTargetAsync(
        CreateMaterialIssueDto dto, CancellationToken ct)
    {
        var (error, target) = dto.WarehouseType == WarehouseTypes.Material
            ? await ResolveCuttingAsync(dto, ct)
            : await ResolveSewingAsync(dto, ct);
        if (error is not null) return (error, null);

        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == target!.OrderId, ct);
        if (order is null || OrderStatuses.Closed.Contains(order.Status))
            return (Fail(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed), null);

        return await IsRequestValidAsync(dto, target!, ct)
            ? (null, target)
            : (Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.RequestInvalid), null);
    }

    /// <summary>Cấp vải: theo lệnh cắt còn mở; chỉ cấp các vải có trong định mức của lệnh.</summary>
    private async Task<(Result<MaterialIssueDto>?, IssueTarget?)> ResolveCuttingAsync(CreateMaterialIssueDto dto,
        CancellationToken ct)
    {
        var wo = await _uow.Repository<WorkOrder>().Query().AsNoTracking()
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine).ThenInclude(o => o.ProductVariant)
            .FirstOrDefaultAsync(w => w.WorkOrderId == dto.WorkOrderId, ct);
        if (wo is null) return (WarehouseFailures.NotFound<MaterialIssueDto>(), null);
        if (wo.Status is not (WorkOrderStatuses.Open or WorkOrderStatuses.Cutting))
            return (Fail(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderNotIssuable), null);

        var lines = wo.WorkOrderLines.Select(l => new FabricLine(l.OrderLine.ProductVariantId, l.ProductComponentMaterialId)).ToList();
        var fabrics = (await _planner.GetFabricsAsync(lines, ct))
            .Select(n => n.MaterialId).ToHashSet();
        var allInPlan = dto.Lines.All(l => fabrics.Contains(l.MaterialId!.Value));
        return allInPlan
            ? (null, new IssueTarget(wo.OrderId, wo.CuttingTeamId, wo.WorkOrderId))
            : (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.MaterialNotInWorkOrder), null);
    }

    /// <summary>Cấp BTP: tổ nhận phải là tổ may đang hoạt động, dòng đơn phải thuộc đơn.</summary>
    private async Task<(Result<MaterialIssueDto>?, IssueTarget?)> ResolveSewingAsync(CreateMaterialIssueDto dto,
        CancellationToken ct)
    {
        var teamOk = await _uow.Repository<Team>().Query().AnyAsync(t => t.TeamId == dto.ReceivingTeamId
            && t.TeamType == TeamTypes.Sewing && t.Status == RecordStatus.Active, ct);
        if (!teamOk) return (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.SewingTeamInvalid), null);

        var ids = dto.Lines.Select(l => l.OrderLineId!.Value).Distinct().ToList();
        var variants = await _uow.Repository<OrderLine>().Query()
            .Where(l => ids.Contains(l.OrderLineId) && l.OrderId == dto.OrderId)
            .ToDictionaryAsync(l => l.OrderLineId, l => l.ProductVariantId, ct);
        if (variants.Count != ids.Count)
            return (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OrderLineNotInOrder), null);

        // THROW 50041: chi tiết lẻ phải là dòng định mức vải (PANEL) của đúng màu × size của dòng đơn.
        var pieceLines = dto.Lines.Where(l => l.ProductComponentMaterialId is not null).ToList();
        if (pieceLines.Count > 0)
        {
            var normIds = pieceLines.Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToList();
            var norms = await _uow.Repository<ProductComponentMaterial>().Query()
                .Where(p => normIds.Contains(p.ProductComponentMaterialId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                            && p.Material.MaterialType == MaterialTypes.Fabric)
                .ToDictionaryAsync(p => p.ProductComponentMaterialId, p => p.ProductVariantId, ct);
            if (pieceLines.Any(l => !norms.TryGetValue(l.ProductComponentMaterialId!.Value, out var v) || v != variants[l.OrderLineId!.Value]))
                return (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ComponentNotInVariantNorm), null);
        }
        return (null, new IssueTarget(dto.OrderId!.Value, dto.ReceivingTeamId!.Value, null));
    }

    /// <summary>
    /// Cấp BTP cho tổ may bắt buộc có đơn Xin BTP; cấp vải cho tổ cắt thì đơn Xin NVL là tùy chọn (UC40).
    /// Có RequestId thì đơn phải đúng loại, đang chờ xử lý (đơn xin không qua bước duyệt), của đúng tổ nhận
    /// và cùng đơn hàng.
    /// </summary>
    private async Task<bool> IsRequestValidAsync(CreateMaterialIssueDto dto, IssueTarget target, CancellationToken ct)
    {
        if (dto.RequestId is null) return dto.WarehouseType == WarehouseTypes.Material;
        var type = dto.WarehouseType == WarehouseTypes.Material
            ? RequestTypes.MaterialRequisition
            : RequestTypes.SemiFinishedRequisition;
        return await _uow.Repository<Request>().Query().AnyAsync(r => r.RequestId == dto.RequestId
            && r.RequestTypeCode == type && r.Status == RequestStatuses.PendingProcessing
            && r.CreatedByTeamId == target.TeamId
            && (r.OrderId == null || r.OrderId == target.OrderId), ct);
    }

    private static StockKey KeyOf(string warehouseType, MaterialIssueLine line) =>
        warehouseType == WarehouseTypes.Material
            ? StockKey.Material(line.MaterialId!.Value)
            : StockKey.SemiFinished(line.OrderLineId!.Value, line.ProductComponentMaterialId);

    private static Result<MaterialIssueDto> Fail(string code, string message) =>
        Result<MaterialIssueDto>.Failure(code, message);
}
