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
    private const int MaxMissingOperationsShown = 5;

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
                    l.OrderLine == null ? null : l.OrderLine.ProductVariant.ProductColor.Color, l.OrderLine == null ? null : l.OrderLine.ProductVariant.SizeCode,
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
        var (requestError, request) = await LoadRequestAsync(dto, ct);
        if (requestError is not null) return requestError;
        var (fillError, filled) = FillFromRequest(dto, request);
        if (fillError is not null) return fillError;
        dto = filled!;

        var (error, target) = await ResolveTargetAsync(dto, request, ct);
        if (error is not null) return error;
        var linesError = await CheckLinesAsync(dto, request, target!, ct);
        if (linesError is not null) return linesError;

        var created = await _uow.InTransactionAsync(() => StoreIssueAsync(dto, issuedByEmployeeId, target!, ct));
        if (!created.IsSuccess) return WarehouseFailures.From<MaterialIssueDto>(created);
        _logger.LogInformation("Material issue {IssueId} ({WarehouseType}) created for team {TeamId}",
            created.Data, dto.WarehouseType, target!.TeamId);
        return await GetByIdAsync(created.Data, null, ct);
    }

    public Task<Result> ConfirmAsync(int issueId, ConfirmIssueDto dto, int teamId, int employeeId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var issue = await Issues.Include(i => i.MaterialIssueLines).FirstOrDefaultAsync(i => i.MaterialIssueId == issueId, ct);
            var check = CheckPending(issue, teamId) ?? await CheckOperationsAssignedAsync(issue!, ct);
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
    /// Tổ may phải phân công đủ trước khi nhận BTP (DEV-ALL-48): mọi công đoạn may đang dùng của các mã hàng trên phiếu đều có ít nhất
    /// một công nhân đang làm của tổ. Mã hàng chưa khai công đoạn cũng bị chặn vì chưa phân công được.
    /// </summary>
    private async Task<Result?> CheckOperationsAssignedAsync(MaterialIssue issue, CancellationToken ct)
    {
        if (issue.WarehouseType != WarehouseTypes.SemiFinished) return null;
        var lineIds = issue.MaterialIssueLines.Where(l => l.OrderLineId != null).Select(l => l.OrderLineId!.Value).ToList();
        var products = await _uow.Repository<OrderLine>().Query().AsNoTracking().Where(l => lineIds.Contains(l.OrderLineId))
            .Select(l => new { l.ProductId, l.ProductVariant.Product.ProductCode }).Distinct().ToListAsync(ct);
        var productIds = products.Select(p => p.ProductId).ToList();
        var operations = _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => productIds.Contains(o.ProductId) && o.Status == OperationStatuses.Active && o.TeamType == TeamTypes.Sewing);

        var withOperations = await operations.Select(o => o.ProductId).Distinct().ToListAsync(ct);
        var noOperations = products.Where(p => !withOperations.Contains(p.ProductId)).Select(p => p.ProductCode).ToList();
        if (noOperations.Count > 0)
            return Result.Failure(ErrorCodes.AssignmentIncomplete, string.Format(Messages.AssignmentNoOperations, string.Join(", ", noOperations)));

        var assignments = _uow.Repository<OperationAssignment>().Query();
        var missing = await operations
            .Where(o => !assignments.Any(a => a.OperationId == o.OperationId && a.OrderId == issue.OrderId
                                                 && a.SewingTeamId == issue.ReceivingTeamId && a.ValidTo == null
                                                 && a.Worker.Status == RecordStatus.Active && a.Worker.TeamId == issue.ReceivingTeamId))
            .OrderBy(o => o.ProductId).ThenBy(o => o.StationSequence)
            .Select(o => o.OperationCode).ToListAsync(ct);
        if (missing.Count == 0) return null;

        var names = missing.Count <= MaxMissingOperationsShown
            ? string.Join(", ", missing)
            : $"{string.Join(", ", missing.Take(MaxMissingOperationsShown))}...";
        return Result.Failure(ErrorCodes.AssignmentIncomplete, string.Format(Messages.AssignmentIncomplete, missing.Count, names));
    }

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

    /// <summary>Kiểm tồn rồi lưu phiếu cấp; phiếu cấp theo đơn xin thì đánh dấu đơn xin hoàn thành và báo tổ trưởng tổ nhận.</summary>
    private async Task<Result<int>> StoreIssueAsync(CreateMaterialIssueDto dto, int issuedByEmployeeId, IssueTarget target, CancellationToken ct)
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
            OrderId = target.OrderId,
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
    }

    /// <summary>Đơn xin (nếu chọn) phải đúng loại và đã được duyệt; chưa lập phiếu cấp nào cho đơn (đơn xin sang Hoàn thành khi lập phiếu).</summary>
    private async Task<(Result<MaterialIssueDto>? Error, Request? Request)> LoadRequestAsync(CreateMaterialIssueDto dto,
        CancellationToken ct)
    {
        if (dto.RequestId is null) return (null, null);
        var type = dto.WarehouseType == WarehouseTypes.Material
            ? RequestTypes.MaterialRequisition
            : RequestTypes.SemiFinishedRequisition;
        var request = await _uow.Repository<Request>().Query().AsNoTracking().Include(r => r.RequestLines)
            .FirstOrDefaultAsync(r => r.RequestId == dto.RequestId && r.RequestTypeCode == type
                                      && r.Status == RequestStatuses.Approved, ct);
        return request is null
            ? (Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.RequestInvalid), null)
            : (null, request);
    }

    /// <summary>
    /// Chọn đơn xin thì lệnh cắt, tổ nhận và đơn hàng lấy từ đơn xin nếu chưa chọn; đã chọn thì phải khớp (không để chọn lệnh
    /// cắt này nhưng đơn xin của lệnh cắt khác, DEV-ALL-99).
    /// </summary>
    private static (Result<MaterialIssueDto>? Error, CreateMaterialIssueDto? Dto) FillFromRequest(CreateMaterialIssueDto dto,
        Request? request)
    {
        if (request is null) return (null, dto);
        var isMaterial = dto.WarehouseType == WarehouseTypes.Material;
        if (isMaterial && request.WorkOrderId is { } requestWo && dto.WorkOrderId is { } chosenWo && requestWo != chosenWo)
            return (Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.IssueRequestWorkOrderMismatch), null);
        if (request.CreatedByTeamId is { } requestTeam && dto.ReceivingTeamId is { } chosenTeam && requestTeam != chosenTeam)
            return (Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.IssueTeamMismatch), null);
        if (request.OrderId is { } requestOrder && dto.OrderId is { } chosenOrder && requestOrder != chosenOrder)
            return (Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.RequestInvalid), null);
        return (null, dto with
        {
            WorkOrderId = dto.WorkOrderId ?? (isMaterial ? request.WorkOrderId : null),
            ReceivingTeamId = dto.ReceivingTeamId ?? request.CreatedByTeamId,
            OrderId = dto.OrderId ?? request.OrderId,
        });
    }

    private async Task<(Result<MaterialIssueDto>? Error, IssueTarget? Target)> ResolveTargetAsync(
        CreateMaterialIssueDto dto, Request? request, CancellationToken ct)
    {
        var (error, target) = dto.WarehouseType != WarehouseTypes.Material ? await ResolveSewingAsync(dto, ct)
            : dto.WorkOrderId is not null ? await ResolveCuttingAsync(dto, ct)
            : await ResolveDirectMaterialAsync(dto, ct);
        if (error is not null) return (error, null);
        if ((dto.ReceivingTeamId is { } team && team != target!.TeamId) || (dto.OrderId is { } chosen && chosen != target!.OrderId))
            return (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.IssueTeamMismatch), null);

        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == target!.OrderId, ct);
        if (order is null || OrderStatuses.Closed.Contains(order.Status))
            return (Fail(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed), null);
        var dateError = DocumentDateRules.CheckDocumentDate(dto.IssueDate, _clock.Today, order.ReceivedDate, WarehouseMessages.LabelIssueDate);
        if (dateError is not null)
            return (Result<MaterialIssueDto>.Failure(ErrorCodes.WarehouseInvalidReference, dateError, ResultKind.Validation), null);

        return IsRequestValid(request, target!)
            ? (null, target)
            : (Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.RequestInvalid), null);
    }

    /// <summary>Cấp NVL không theo lệnh cắt (DEV-ALL-99): tổ nhận là tổ cắt hoặc tổ may đang hoạt động, đơn hàng bắt buộc.</summary>
    private async Task<(Result<MaterialIssueDto>?, IssueTarget?)> ResolveDirectMaterialAsync(CreateMaterialIssueDto dto,
        CancellationToken ct)
    {
        if (dto.OrderId is null || dto.ReceivingTeamId is null)
            return (Result<MaterialIssueDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.IssueTargetRequired,
                ResultKind.Validation), null);
        var teamOk = await _uow.Repository<Team>().Query().AnyAsync(t => t.TeamId == dto.ReceivingTeamId
            && (t.TeamType == TeamTypes.Cutting || t.TeamType == TeamTypes.Sewing) && t.Status == RecordStatus.Active, ct);
        return teamOk
            ? (null, new IssueTarget(dto.OrderId.Value, dto.ReceivingTeamId.Value, null))
            : (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.IssueTeamInvalid), null);
    }

    /// <summary>
    /// Kiểm dòng cấp. NVL phải còn dùng; vải lẻ tối đa 2 chữ số, NVL khác (chỉ, cúc…) số nguyên. Có đơn xin thì chỉ cấp các dòng
    /// có trong đơn và không quá số lượng đơn xin.
    /// </summary>
    private async Task<Result<MaterialIssueDto>?> CheckLinesAsync(CreateMaterialIssueDto dto, Request? request, IssueTarget target,
        CancellationToken ct)
    {
        if (dto.WarehouseType == WarehouseTypes.Material)
        {
            var ids = dto.Lines.Select(l => l.MaterialId!.Value).Distinct().ToList();
            var materials = await _uow.Repository<Material>().Query().AsNoTracking()
                .Where(m => ids.Contains(m.MaterialId) && m.Status == MaterialStatuses.Active)
                .Select(m => new { m.MaterialId, m.MaterialName, m.MaterialType }).ToDictionaryAsync(m => m.MaterialId, ct);
            foreach (var line in dto.Lines)
            {
                if (!materials.TryGetValue(line.MaterialId!.Value, out var material))
                    return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.IssueMaterialInvalid);
                if (!MaterialRequisitionRequestHandler.IsQuantityValid(material.MaterialType == MaterialTypes.Fabric, line.IssuedQty))
                    return Result<MaterialIssueDto>.Failure(ErrorCodes.WarehouseInvalidReference,
                        material.MaterialType == MaterialTypes.Fabric
                            ? string.Format(WarehouseMessages.IssueFabricQtyInvalid, material.MaterialName, RequisitionLimits.MaxMaterialQty)
                            : string.Format(WarehouseMessages.IssueOtherQtyInvalid, material.MaterialName, RequisitionLimits.MaxMaterialQty),
                        ResultKind.Validation);
            }
            // Cấp theo đơn hàng (không theo lệnh cắt): chỉ cấp NVL đã khai trong NVL của đơn (DEV-ALL-99). Theo lệnh cắt thì
            // ResolveCuttingAsync đã giới hạn trong vải của lệnh.
            if (target.WorkOrderId is null)
            {
                var ofOrder = await _uow.Repository<OrderMaterial>().Query().AsNoTracking()
                    .Where(m => m.OrderId == target.OrderId && ids.Contains(m.MaterialId)).Select(m => m.MaterialId).ToListAsync(ct);
                var outside = ids.Except(ofOrder).Select(id => materials[id].MaterialName).ToList();
                if (outside.Count > 0)
                    return Fail(ErrorCodes.WarehouseInvalidReference,
                        string.Format(WarehouseMessages.IssueMaterialNotInOrder, string.Join(", ", outside)));
            }
        }
        if (request is null) return null;

        var requested = request.RequestLines
            .GroupBy(l => (l.MaterialId, l.OrderLineId)).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        foreach (var line in dto.Lines)
        {
            if (!requested.TryGetValue((line.MaterialId, line.OrderLineId), out var max))
                return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.IssueLineNotInRequest);
            var issued = dto.Lines.Where(l => (l.MaterialId, l.OrderLineId) == (line.MaterialId, line.OrderLineId)).Sum(l => l.IssuedQty);
            if (issued > max)
                return Fail(ErrorCodes.WarehouseInvalidReference, string.Format(WarehouseMessages.IssueQtyOverRequest, max.ToString("#,0.###")));
        }
        return null;
    }

    /// <summary>Cấp vải: theo lệnh cắt còn mở; chỉ cấp các vải có trong định mức của lệnh.</summary>
    private async Task<(Result<MaterialIssueDto>?, IssueTarget?)> ResolveCuttingAsync(CreateMaterialIssueDto dto,
        CancellationToken ct)
    {
        var wo = await _uow.Repository<WorkOrder>().Query().AsNoTracking()
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine).ThenInclude(o => o.ProductVariant).ThenInclude(v => v.ProductColor)
            .FirstOrDefaultAsync(w => w.WorkOrderId == dto.WorkOrderId, ct);
        if (wo is null) return (WarehouseFailures.NotFound<MaterialIssueDto>(), null);
        if (wo.Status is not (WorkOrderStatuses.Open or WorkOrderStatuses.Cutting))
            return (Fail(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderNotIssuable), null);

        var lines = wo.WorkOrderLines.Select(l => new FabricLine(l.OrderLine.ProductVariant.ProductColorId, l.ProductComponentMaterialId)).ToList();
        var fabrics = (await _planner.GetFabricsAsync(lines, ct))
            .Select(n => n.MaterialId).ToHashSet();
        var allInPlan = dto.Lines.All(l => fabrics.Contains(l.MaterialId!.Value));
        return allInPlan
            ? (null, new IssueTarget(wo.OrderId, wo.CuttingTeamId, wo.WorkOrderId))
            : (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.MaterialNotInWorkOrder), null);
    }

    /// <summary>Cấp BTP: tổ nhận phải là tổ may đang hoạt động, dòng đơn phải thuộc đơn, mã hàng phải được chia cho tổ (DEV-ALL-49).</summary>
    private async Task<(Result<MaterialIssueDto>?, IssueTarget?)> ResolveSewingAsync(CreateMaterialIssueDto dto,
        CancellationToken ct)
    {
        if (dto.OrderId is null || dto.ReceivingTeamId is null)
            return (Result<MaterialIssueDto>.Failure(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.IssueTargetRequired,
                ResultKind.Validation), null);
        var teamOk = await _uow.Repository<Team>().Query().AnyAsync(t => t.TeamId == dto.ReceivingTeamId
            && t.TeamType == TeamTypes.Sewing && t.Status == RecordStatus.Active, ct);
        if (!teamOk) return (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.SewingTeamInvalid), null);

        var ids = dto.Lines.Select(l => l.OrderLineId!.Value).Distinct().ToList();
        var colors = await _uow.Repository<OrderLine>().Query()
            .Where(l => ids.Contains(l.OrderLineId) && l.OrderId == dto.OrderId)
            .ToDictionaryAsync(l => l.OrderLineId, l => l.ProductVariant.ProductColorId, ct);
        if (colors.Count != ids.Count)
            return (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OrderLineNotInOrder), null);

        var notAllotted = await FindNotAllottedLinesAsync(dto.ReceivingTeamId!.Value, ids, ct);
        if (notAllotted.Count > 0)
            return (Fail(ErrorCodes.AssignmentProductNotAllotted,
                string.Format(WarehouseMessages.ProductNotAllottedToTeam, string.Join(", ", notAllotted))), null);

        // THROW 50041: chi tiết lẻ phải là dòng định mức vải (PANEL) của đúng màu áo của dòng đơn (định mức theo màu, DEV-ALL-87).
        var pieceLines = dto.Lines.Where(l => l.ProductComponentMaterialId is not null).ToList();
        if (pieceLines.Count > 0)
        {
            var normIds = pieceLines.Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToList();
            var norms = await _uow.Repository<ProductComponentMaterial>().Query()
                .Where(p => normIds.Contains(p.ProductComponentMaterialId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                            && p.Material.MaterialType == MaterialTypes.Fabric)
                .ToDictionaryAsync(p => p.ProductComponentMaterialId, p => p.ProductColorId, ct);
            if (pieceLines.Any(l => !norms.TryGetValue(l.ProductComponentMaterialId!.Value, out var c) || c != colors[l.OrderLineId!.Value]))
                return (Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ComponentNotInVariantNorm), null);
        }
        return (null, new IssueTarget(dto.OrderId!.Value, dto.ReceivingTeamId!.Value, null));
    }

    private async Task<List<string>> FindNotAllottedLinesAsync(int teamId, IReadOnlyCollection<int> orderLineIds, CancellationToken ct)
    {
        var allotted = _uow.Repository<OrderLineTeam>().Query().Where(a => a.SewingTeamId == teamId).Select(a => a.OrderLineId);
        var lines = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => orderLineIds.Contains(l.OrderLineId) && !allotted.Contains(l.OrderLineId))
            .OrderBy(l => l.ProductVariant.Product.ProductCode).ThenBy(l => l.ProductVariant.ProductColor.Color).ThenBy(l => l.ProductVariant.SizeCode)
            .Select(l => new { l.ProductVariant.Product.ProductCode, l.ProductVariant.ProductColor.Color, l.ProductVariant.SizeCode }).ToListAsync(ct);
        var labels = lines.Take(MaxMissingOperationsShown)
            .Select(l => string.Format(Messages.OrderLineLabel, l.ProductCode, l.Color, l.SizeCode)).ToList();
        if (lines.Count > MaxMissingOperationsShown) labels.Add("...");
        return labels;
    }

    /// <summary>
    /// Đơn xin là tùy chọn cho cả phiếu cấp NVL và BTP (DEV-ALL-99). Có đơn xin thì đơn (đã loại, đã duyệt kiểm ở LoadRequestAsync)
    /// phải của đúng tổ nhận, cùng đơn hàng và cùng lệnh cắt (nếu đơn xin gắn lệnh).
    /// </summary>
    private static bool IsRequestValid(Request? request, IssueTarget target) =>
        request is null
        || (request.CreatedByTeamId == target.TeamId
            && (request.OrderId == null || request.OrderId == target.OrderId)
            && (request.WorkOrderId == null || request.WorkOrderId == target.WorkOrderId));

    private static StockKey KeyOf(string warehouseType, MaterialIssueLine line) =>
        warehouseType == WarehouseTypes.Material
            ? StockKey.Material(line.MaterialId!.Value)
            : StockKey.SemiFinished(line.OrderLineId!.Value, line.ProductComponentMaterialId);

    private static Result<MaterialIssueDto> Fail(string code, string message) =>
        Result<MaterialIssueDto>.Failure(code, message);
}
