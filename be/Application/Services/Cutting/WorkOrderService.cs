using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC19 (tổ cắt): Quản đốc lập lệnh cắt theo dòng đơn; lệnh cắt bù thiếu vải (ước tính) thì chặn và nêu rõ vải thiếu.</summary>
public sealed class WorkOrderService : IWorkOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly FabricPlanner _planner;
    private readonly IDocumentCodeGenerator _codes;
    private readonly INotificationService _notifier;
    private readonly ILogger<WorkOrderService> _logger;
    private readonly IClock _clock;
    private readonly WorkOrderFabricChecker _fabricChecker;

    public WorkOrderService(IUnitOfWork uow, IStockService stock, FabricPlanner planner, IDocumentCodeGenerator codes,
        INotificationService notifier, ILogger<WorkOrderService> logger, IClock clock, WorkOrderFabricChecker fabricChecker)
    {
        _uow = uow; _stock = stock; _planner = planner; _codes = codes; _notifier = notifier; _logger = logger; _clock = clock;
        _fabricChecker = fabricChecker;
        _needs = new WorkOrderFabricNeeds(uow, fabricChecker);
    }

    private readonly WorkOrderFabricNeeds _needs;

    private const string WorkOrderLink = "/cutting/work-orders";

    /// <summary>Lệnh cắt mới: tổ cắt biết để chuẩn bị, Thủ kho là người xử lý tiếp (cấp vải cho tổ).</summary>
    private async Task NotifyCreatedAsync(WorkOrder wo, string? orderCode, CancellationToken ct)
    {
        var where = orderCode is null ? string.Empty : $" (đơn {orderCode})";
        await _notifier.NotifyTeamLeadersAsync(wo.CuttingTeamId, new NotificationMessage(NotificationSources.Production,
            "Có lệnh cắt mới", $"Quản đốc giao lệnh cắt {wo.WorkOrderCode}{where}, hạn {wo.DueDate:dd/MM/yyyy}.", WorkOrderLink), ct);
        await _notifier.NotifyRoleAsync(RoleCodes.WarehouseKeeper, new NotificationMessage(NotificationSources.Warehouse,
            "Cần cấp vải cho lệnh cắt", $"Lệnh cắt {wo.WorkOrderCode}{where} đã lập, vui lòng lập phiếu cấp vải cho tổ cắt.", WorkOrderLink), ct);
    }

    private IQueryable<WorkOrder> WorkOrders => _uow.Repository<WorkOrder>().Query();

    public async Task<Result<PagedResult<WorkOrderListItemDto>>> GetPagedAsync(WorkOrderQuery query,
        int? cuttingTeamId, CancellationToken ct)
    {
        var keyword = query.Keyword?.Trim();
        var page = await WorkOrders.AsNoTracking()
            .Where(w => cuttingTeamId == null || w.CuttingTeamId == cuttingTeamId)
            .Where(w => string.IsNullOrEmpty(query.Status) || w.Status == query.Status)
            .Where(w => query.OrderId == null || w.OrderId == query.OrderId)
            .Where(w => string.IsNullOrEmpty(keyword) || w.WorkOrderCode.Contains(keyword)
                        || w.Order.OrderCode.Contains(keyword) || w.Product.ProductCode.Contains(keyword))
            .OrderByDescending(w => w.WorkOrderId)
            .Select(w => new WorkOrderListItemDto(w.WorkOrderId, w.WorkOrderCode, w.OrderId, w.Order.OrderCode,
                w.ProductId, w.Product.ProductCode, w.CuttingTeamId, w.CuttingTeam.TeamName, w.DueDate, w.Status,
                w.WorkOrderLines.Sum(l => l.PlannedQty), w.CreatedAt))
            .ToPagedAsync(query, ct);
        return Result<PagedResult<WorkOrderListItemDto>>.Success(page);
    }

    public async Task<Result<WorkOrderDto>> GetByIdAsync(int workOrderId, int? cuttingTeamId, CancellationToken ct)
    {
        var wo = await WorkOrders.AsNoTracking().Include(w => w.Order).Include(w => w.Product).Include(w => w.CuttingTeam)
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine).ThenInclude(o => o.ProductVariant).ThenInclude(v => v.ProductColor)
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.ProductComponentMaterial!).ThenInclude(p => p.ProductComponent)
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.ProductComponentMaterial!).ThenInclude(p => p.Material)
            .FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound<WorkOrderDto>();
        if (cuttingTeamId is not null && wo.CuttingTeamId != cuttingTeamId) return WarehouseFailures.TeamMismatch<WorkOrderDto>();

        var cut = await _uow.Repository<CuttingLayLine>().Query().Where(l => l.CuttingLay.WorkOrderId == workOrderId)
            .GroupBy(l => new { l.OrderLineId, l.ProductComponentMaterialId }).Select(g => new { g.Key, Qty = g.Sum(l => l.ActualQty) })
            .ToDictionaryAsync(x => (x.Key.OrderLineId, x.Key.ProductComponentMaterialId), x => x.Qty, ct);
        var handed = await HandedQtyAsync(workOrderId, ct);
        var fabricsOf = await _planner.FabricIdsByLineAsync(wo.WorkOrderLines.Select(FabricLineOf), ct);
        var lines = wo.WorkOrderLines.OrderBy(l => l.OrderLine.ProductVariant.ProductColor.Color).ThenBy(l => l.OrderLine.ProductVariant.SizeCode)
            .ThenBy(l => l.ProductComponentMaterial == null ? 0 : l.ProductComponentMaterial.ProductComponentId)
            .Select(l => new WorkOrderLineDto(l.WorkOrderLineId, l.OrderLineId, l.OrderLine.ProductVariant.ProductColor.Color,
                l.OrderLine.ProductVariant.SizeCode, l.ProductComponentMaterialId, ComponentLabels.Of(l.ProductComponentMaterial) ?? string.Empty,
                l.PlannedQty, cut.GetValueOrDefault((l.OrderLineId, l.ProductComponentMaterialId)),
                handed.GetValueOrDefault((l.OrderLineId, l.ProductComponentMaterialId)), fabricsOf[FabricLineOf(l)]))
            .ToList();

        return Result<WorkOrderDto>.Success(new WorkOrderDto(wo.WorkOrderId, wo.WorkOrderCode, wo.OrderId,
            wo.Order.OrderCode, wo.ProductId, wo.Product.ProductCode, wo.Product.ProductName, wo.CuttingTeamId,
            wo.CuttingTeam.TeamName, wo.SourceRequestId, wo.DueDate, wo.Status, wo.Note, wo.CreatedAt, lines,
            await GetFabricsAsync(wo, ct), await FabricChecksAsync(wo, ct), wo.FabricTolerancePercent));
    }

    /// <summary>Chỉ kiểm lệnh thường chưa bắt đầu cắt: khi đã cắt, vải tổ đang giữ không còn phản ánh phần còn phải cắt.</summary>
    private async Task<IReadOnlyList<WorkOrderFabricCheckDto>> FabricChecksAsync(WorkOrder wo, CancellationToken ct)
    {
        if (wo.Status != WorkOrderStatuses.Open || wo.SourceRequestId is not null) return [];
        var lines = wo.WorkOrderLines.Select(l => new CutLine(l.OrderLine.ProductVariantId, l.PlannedQty,
            VariantLabel(l.OrderLine.ProductVariant.ProductColor.Color, l.OrderLine.ProductVariant.SizeCode), l.ProductComponentMaterialId)).ToList();
        return await _fabricChecker.CheckAsync(wo.OrderId, wo.CuttingTeamId, lines, ct);
    }

    private static string VariantLabel(string color, string size) => $"{color} - {size}";

    private static FabricLine FabricLineOf(WorkOrderLine l) =>
        new(l.OrderLine.ProductVariant.ProductColorId, l.ProductComponentMaterialId);

    public async Task<Result<IReadOnlyList<WorkOrderFabricCheckDto>>> CheckFabricAsync(CheckWorkOrderFabricDto dto, CancellationToken ct)
    {
        var ids = dto.Lines.Select(l => l.OrderLineId).ToArray();
        var variants = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => ids.Contains(l.OrderLineId) && l.OrderId == dto.OrderId)
            .Select(l => new { l.OrderLineId, l.ProductVariantId, l.ProductVariant.ProductColor.Color, l.ProductVariant.SizeCode })
            .ToDictionaryAsync(l => l.OrderLineId, ct);
        var lines = dto.Lines.Where(l => variants.ContainsKey(l.OrderLineId))
            .Select(l =>
            {
                var v = variants[l.OrderLineId];
                return new CutLine(v.ProductVariantId, l.PlannedQty, VariantLabel(v.Color, v.SizeCode), l.ProductComponentMaterialId);
            }).ToList();
        return Result<IReadOnlyList<WorkOrderFabricCheckDto>>.Success(
            await _fabricChecker.CheckAsync(dto.OrderId, dto.CuttingTeamId, lines, ct));
    }

    public async Task<Result<IReadOnlyList<FabricRequirementDto>>> GetFabricRequirementsAsync(int workOrderId,
        CancellationToken ct)
    {
        var wo = await WorkOrders.AsNoTracking().Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine)
            .ThenInclude(o => o.ProductVariant).ThenInclude(v => v.ProductColor).FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        return wo is null
            ? WarehouseFailures.NotFound<IReadOnlyList<FabricRequirementDto>>()
            : Result<IReadOnlyList<FabricRequirementDto>>.Success(await GetFabricsAsync(wo, ct));
    }

    public async Task<Result<IReadOnlyList<FabricNeedDto>>> GetFabricNeedsAsync(int workOrderId, string? color, string? sizeCode,
        CancellationToken ct)
    {
        var wo = await _needs.LoadAsync(workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound<IReadOnlyList<FabricNeedDto>>();

        var colorKey = color?.Trim();
        var sizeKey = sizeCode?.Trim();
        var lines = wo.WorkOrderLines
            .Where(l => string.IsNullOrEmpty(colorKey) || string.Equals(l.OrderLine.ProductVariant.ProductColor.Color, colorKey, StringComparison.OrdinalIgnoreCase))
            .Where(l => string.IsNullOrEmpty(sizeKey) || string.Equals(l.OrderLine.ProductVariant.SizeCode, sizeKey, StringComparison.OrdinalIgnoreCase));
        var checks = await _needs.NeedsAsync(wo, lines, ct);
        IReadOnlyList<FabricNeedDto> needs = checks.Where(c => c.NeedMeters is > 0)
            .Select(c => new FabricNeedDto(c.MaterialId, c.MaterialCode, c.MaterialName, c.MaterialColor, c.NeedMeters!.Value)).ToList();
        return Result<IReadOnlyList<FabricNeedDto>>.Success(needs);
    }

    public async Task<Result<IReadOnlyList<WorkOrderMaterialAllowanceDto>>> GetMaterialAllowanceAsync(int workOrderId, int cuttingTeamId,
        int? exceptRequestId, CancellationToken ct)
    {
        var wo = await _needs.LoadAsync(workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound<IReadOnlyList<WorkOrderMaterialAllowanceDto>>();
        if (wo.CuttingTeamId != cuttingTeamId) return WarehouseFailures.TeamMismatch<IReadOnlyList<WorkOrderMaterialAllowanceDto>>();
        return Result<IReadOnlyList<WorkOrderMaterialAllowanceDto>>.Success(await _needs.AllowanceAsync(wo, exceptRequestId, ct));
    }

    public async Task<Result<WorkOrderDto>> CreateAsync(SaveWorkOrderDto dto, CancellationToken ct)
    {
        var check = await ValidateAsync(dto, null, ct);
        if (check is not null) return check;

        var created = await _uow.InTransactionAsync(async () =>
        {
            var wo = new WorkOrder
            {
                WorkOrderCode = await _codes.NextAsync<WorkOrder>(DocumentPrefixes.WorkOrder, w => w.WorkOrderCode, ct),
                Status = WorkOrderStatuses.Open,
            };
            Apply(wo, dto);
            await _uow.Repository<WorkOrder>().AddAsync(wo);
            await NotifyCreatedAsync(wo, await OrderCodeAsync(dto.OrderId, ct), ct);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(wo.WorkOrderId);
        });
        _logger.LogInformation("Work order {WorkOrderId} created for order {OrderId}", created.Data, dto.OrderId);
        return await GetByIdAsync(created.Data, null, ct);
    }

    public async Task<Result<IReadOnlyList<ReplacementRequestDto>>> GetOpenReplacementRequestsAsync(CancellationToken ct)
    {
        var withOrder = _uow.Repository<WorkOrder>().Query()
            .Where(w => w.SourceRequestId != null && w.Status != WorkOrderStatuses.Cancelled)
            .Select(w => w.SourceRequestId!.Value);
        var requests = await _uow.Repository<Request>().Query().AsNoTracking()
            .Where(r => r.RequestTypeCode == RequestTypes.ReplacementCutting && r.Status == RequestStatuses.Approved
                        && !withOrder.Contains(r.RequestId))
            .OrderBy(r => r.RequestId)
            .Select(r => new ReplacementRequestDto(r.RequestId, r.RequestCode, r.OrderId!.Value, r.Order!.OrderCode,
                r.RequestLines.Select(l => l.OrderLine!.ProductVariant.Product.ProductCode).FirstOrDefault() ?? string.Empty, r.Content,
                r.RequestLines.OrderBy(l => l.RequestLineId).Select(l => new ReplacementRequestLineDto(l.OrderLineId!.Value,
                    l.OrderLine!.ProductVariant.ProductColor.Color, l.OrderLine.ProductVariant.SizeCode, l.ProductComponentMaterialId!.Value,
                    l.ProductComponentMaterial!.ProductComponent.ComponentName + " – " + l.ProductComponentMaterial.Material.MaterialName + (l.ProductComponentMaterial.Material.Color == "" ? "" : " " + l.ProductComponentMaterial.Material.Color), (int)l.Quantity)).ToList()))
            .ToListAsync(ct);
        return Result<IReadOnlyList<ReplacementRequestDto>>.Success(requests);
    }

    public async Task<Result<WorkOrderDto>> CreateReplacementAsync(CreateReplacementWorkOrderDto dto, CancellationToken ct)
    {
        var request = await _uow.Repository<Request>().Query().AsNoTracking().Include(r => r.RequestLines)
            .FirstOrDefaultAsync(r => r.RequestId == dto.RequestId && r.RequestTypeCode == RequestTypes.ReplacementCutting, ct);
        var check = await ValidateReplacementAsync(request, dto, ct);
        if (check.Failure is not null) return check.Failure;

        var created = await _uow.InTransactionAsync(() => AddReplacementAsync(dto, request!, check, ct));
        _logger.LogInformation("Replacement work order {WorkOrderId} created from request {RequestId}", created.Data, dto.RequestId);
        return await GetByIdAsync(created.Data, null, ct);
    }

    private sealed record ReplacementCheck(Result<WorkOrderDto>? Failure, int ProductId = 0, string OrderCode = "");

    /// <summary>
    /// Đơn cắt bù phải đã duyệt, chưa có lệnh, có đủ dòng; đơn hàng chưa đóng, tổ cắt hợp lệ, các dòng thuộc đơn và cùng một mã hàng,
    /// vải đủ cho phần cắt bù. Trả về mã hàng và mã đơn để tạo lệnh.
    /// </summary>
    private async Task<ReplacementCheck> ValidateReplacementAsync(Request? request, CreateReplacementWorkOrderDto dto, CancellationToken ct)
    {
        if (request is null || request.OrderId is null) return new ReplacementCheck(WarehouseFailures.NotFound<WorkOrderDto>());
        if (request.Status != RequestStatuses.Approved)
            return new ReplacementCheck(Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.ReplacementRequestNotApproved));
        var hasOrder = await WorkOrders.AnyAsync(w => w.SourceRequestId == request.RequestId && w.Status != WorkOrderStatuses.Cancelled, ct);
        if (hasOrder) return new ReplacementCheck(Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.ReplacementOrderExists));
        if (request.RequestLines.Count == 0 || request.RequestLines.Any(l => l.OrderLineId is null || l.ProductComponentMaterialId is null))
            return new ReplacementCheck(Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.ReplacementLinesMissing));

        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking().FirstAsync(o => o.OrderId == request.OrderId, ct);
        if (OrderStatuses.Closed.Contains(order.Status)) return new ReplacementCheck(Fail(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed));
        if (dto.DueDate < _clock.Today)
            return new ReplacementCheck(Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.WorkOrderDueInPast));
        if (dto.DueDate <= order.ReceivedDate)
            return new ReplacementCheck(Fail(ErrorCodes.WarehouseInvalidReference,
                string.Format(WarehouseMessages.WorkOrderDueBeforeStart, order.ReceivedDate.ToString("dd/MM/yyyy"))));
        var teamOk = await _uow.Repository<Team>().Query().AnyAsync(t => t.TeamId == dto.CuttingTeamId
            && t.TeamType == TeamTypes.Cutting && t.Status == RecordStatus.Active, ct);
        if (!teamOk) return new ReplacementCheck(Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.CuttingTeamInvalid));

        var lineIds = request.RequestLines.Select(l => l.OrderLineId!.Value).Distinct().ToList();
        var orderLines = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Include(l => l.ProductVariant).ThenInclude(v => v.ProductColor)
            .Where(l => lineIds.Contains(l.OrderLineId) && l.OrderId == request.OrderId).ToDictionaryAsync(l => l.OrderLineId, ct);
        if (orderLines.Count != lineIds.Count) return new ReplacementCheck(Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OrderLineNotInOrder));
        var productIds = orderLines.Values.Select(l => l.ProductId).Distinct().ToList();
        if (productIds.Count != 1) return new ReplacementCheck(Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ProductNotInOrder));

        // Vải của lệnh cắt bù chỉ cảnh báo, không chặn (hệ thống ước tính, có thể chênh lệch với thực tế): xem kiểm vải ở chi tiết lệnh.
        return new ReplacementCheck(null, productIds[0], order.OrderCode);
    }

    private async Task<Result<int>> AddReplacementAsync(CreateReplacementWorkOrderDto dto, Request request, ReplacementCheck check,
        CancellationToken ct)
    {
        var wo = new WorkOrder
        {
            WorkOrderCode = await _codes.NextAsync<WorkOrder>(DocumentPrefixes.WorkOrder, w => w.WorkOrderCode, ct),
            Status = WorkOrderStatuses.Open,
            OrderId = request.OrderId!.Value,
            ProductId = check.ProductId,
            CuttingTeamId = dto.CuttingTeamId,
            DueDate = dto.DueDate,
            SourceRequestId = request.RequestId,
            Note = string.IsNullOrWhiteSpace(dto.Note) ? $"Cắt bù theo {request.RequestCode}" : dto.Note.Trim(),
            WorkOrderLines = request.RequestLines.Select(l => new WorkOrderLine
            {
                OrderLineId = l.OrderLineId!.Value,
                ProductComponentMaterialId = l.ProductComponentMaterialId,
                PlannedQty = (int)l.Quantity,
            }).ToList(),
        };
        await _uow.Repository<WorkOrder>().AddAsync(wo);
        await NotifyCreatedAsync(wo, check.OrderCode, ct);
        await _uow.SaveChangesAsync();
        return Result<int>.Success(wo.WorkOrderId);
    }

    public async Task<Result<WorkOrderDto>> UpdateAsync(int workOrderId, SaveWorkOrderDto dto, CancellationToken ct)
    {
        var wo = await WorkOrders.Include(w => w.WorkOrderLines).FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound<WorkOrderDto>();
        var editable = await EnsureEditableAsync(wo, ct);
        if (!editable.IsSuccess) return WarehouseFailures.From<WorkOrderDto>(editable);

        var check = await ValidateAsync(dto, workOrderId, ct, wo.DueDate);
        if (check is not null) return check;

        _uow.Repository<WorkOrderLine>().RemoveRange(wo.WorkOrderLines.ToList());
        Apply(wo, dto);
        await _notifier.NotifyTeamLeadersAsync(wo.CuttingTeamId, new NotificationMessage(NotificationSources.Production,
            "Lệnh cắt được sửa", $"Quản đốc đã sửa lệnh cắt {wo.WorkOrderCode}, xem lại kế hoạch.", WorkOrderLink), ct);
        await _uow.SaveChangesAsync();
        return await GetByIdAsync(workOrderId, null, ct);
    }

    public async Task<Result> CancelAsync(int workOrderId, CancellationToken ct)
    {
        var wo = await WorkOrders.FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound();
        var editable = await EnsureEditableAsync(wo, ct);
        if (!editable.IsSuccess) return editable;

        wo.Status = WorkOrderStatuses.Cancelled;
        await _notifier.NotifyTeamLeadersAsync(wo.CuttingTeamId, new NotificationMessage(NotificationSources.Production,
            "Lệnh cắt bị hủy", $"Quản đốc đã hủy lệnh cắt {wo.WorkOrderCode}.", WorkOrderLink), ct);
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.WorkOrderCancelled);
    }

    /// <summary>
    /// Quản đốc / Admin đóng lệnh cắt thiếu (DEV-ALL-52): cắt đủ thì lệnh đã tự hoàn thành khi kho nhận đủ (WorkOrderCompletion),
    /// nên chỉ dùng khi không cắt đủ được. Phải hết phiếu bàn giao / trả vải chờ kho và BTP đã cắt đã vào kho hết.
    /// </summary>
    public async Task<Result> CloseShortAsync(int workOrderId, string reason, CancellationToken ct)
    {
        var wo = await WorkOrders.Include(w => w.WorkOrderLines).Include(w => w.Order)
            .FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound();
        if (wo.Status != WorkOrderStatuses.Cutting)
            return Result.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderNotFinishable);

        reason = reason.Trim();
        var check = await WorkOrderFinishCheck.RunAsync(_uow, wo, reason, ct);
        if (check.Failure is not null) return check.Failure;
        var (inWarehouse, planned) = (check.InWarehouse, check.Planned);

        var note = AppendShortReason(wo.Note, reason);
        if (note.Length > MaxNoteLength)
            return Result.Failure(ErrorCodes.CommonValidation, WarehouseMessages.WorkOrderNoteTooLongToAppend, ResultKind.Validation);
        wo.Status = WorkOrderStatuses.Completed;
        wo.CompletedAt = _clock.Now;
        wo.Note = note.Length == 0 ? null : note;
        await _notifier.NotifyTeamLeadersAsync(wo.CuttingTeamId, new NotificationMessage(NotificationSources.Production, "Lệnh cắt đã đóng",
            $"Quản đốc đã đóng lệnh {wo.WorkOrderCode} (đơn {wo.Order.OrderCode}) khi cắt {inWarehouse}/{planned}: {reason}.", WorkOrderLink), ct);
        await _uow.SaveChangesAsync();
        _logger.LogInformation("Work order {WorkOrderId} closed short: {InWarehouse}/{Planned}", workOrderId, inWarehouse, planned);
        return Result.Success(WarehouseMessages.WorkOrderFinishedCutting);
    }

    private const int MaxNoteLength = 1000;

    /// <summary>Ghi chú sau khi nối lý do cắt thiếu; không có lý do thì giữ nguyên ghi chú (null khi trống).</summary>
    private static string AppendShortReason(string? note, string? reason)
    {
        if (string.IsNullOrEmpty(reason)) return note ?? string.Empty;
        return string.IsNullOrEmpty(note) ? $"Cắt thiếu: {reason}" : $"{note}\nCắt thiếu: {reason}";
    }

    /// <summary>Chỉ sửa / hủy khi lệnh còn OPEN, chưa có bàn cắt và chưa có phiếu cấp (trừ phiếu bị từ chối).</summary>
    private async Task<Result> EnsureEditableAsync(WorkOrder wo, CancellationToken ct)
    {
        if (wo.Status != WorkOrderStatuses.Open) return WarehouseFailures.InvalidStatus();
        var hasLays = await _uow.Repository<CuttingLay>().Query().AnyAsync(l => l.WorkOrderId == wo.WorkOrderId, ct);
        var hasIssues = await _uow.Repository<MaterialIssue>().Query()
            .AnyAsync(i => i.WorkOrderId == wo.WorkOrderId && i.Status != IssueStatuses.Rejected, ct);
        return hasLays || hasIssues
            ? Result.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderHasActivity)
            : Result.Success();
    }

    private async Task<Result<WorkOrderDto>?> ValidateAsync(SaveWorkOrderDto dto, int? exceptWorkOrderId,
        CancellationToken ct, DateOnly? previousDueDate = null)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return WarehouseFailures.NotFound<WorkOrderDto>();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Fail(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);
        // Không đặt hạn trong quá khứ (sửa lệnh cũ mà giữ nguyên hạn thì không kiểm lại).
        if (dto.DueDate < _clock.Today && dto.DueDate != previousDueDate)
            return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.WorkOrderDueInPast);
        // Hạn của lệnh cắt phải sau ngày bắt đầu của đơn (ngày nhận đơn).
        if (dto.DueDate <= order.ReceivedDate)
            return Fail(ErrorCodes.WarehouseInvalidReference, string.Format(WarehouseMessages.WorkOrderDueBeforeStart, order.ReceivedDate.ToString("dd/MM/yyyy")));

        var teamOk = await _uow.Repository<Team>().Query().AnyAsync(t => t.TeamId == dto.CuttingTeamId
            && t.TeamType == TeamTypes.Cutting && t.Status == RecordStatus.Active, ct);
        if (!teamOk) return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.CuttingTeamInvalid);

        var ids = dto.Lines.Select(l => l.OrderLineId).Distinct().ToList();
        var orderLines = await _uow.Repository<OrderLine>().Query().AsNoTracking().Include(l => l.ProductVariant).ThenInclude(v => v.ProductColor)
            .Where(l => ids.Contains(l.OrderLineId) && l.OrderId == dto.OrderId && l.ProductId == dto.ProductId)
            .ToDictionaryAsync(l => l.OrderLineId, ct);
        if (orderLines.Count != ids.Count) return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ProductNotInOrder);

        var components = await CheckExtraComponentsAsync(dto, orderLines, ct);
        return components ?? await CheckPlanCapAsync(dto, orderLines, exceptWorkOrderId, ct);
    }

    /// <summary>Dòng cắt lẻ phải là chi tiết vải (PANEL) đang dùng trong định mức của đúng màu áo (định mức theo màu, DEV-ALL-87) (trùng kiểm tra của trg_WorkOrderLine_Check để báo lỗi rõ).</summary>
    private async Task<Result<WorkOrderDto>?> CheckExtraComponentsAsync(SaveWorkOrderDto dto, IReadOnlyDictionary<int, OrderLine> orderLines, CancellationToken ct)
    {
        var extras = dto.Lines.Where(l => l.ProductComponentMaterialId is not null).ToList();
        if (extras.Count == 0) return null;
        var normIds = extras.Select(l => l.ProductComponentMaterialId!.Value).Distinct().ToArray();
        var colorOf = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(p => normIds.Contains(p.ProductComponentMaterialId) && p.ProductComponent.ComponentType == ComponentTypes.Panel
                        && p.ProductComponent.Status == RecordStatus.Active)
            .ToDictionaryAsync(p => p.ProductComponentMaterialId, p => p.ProductColorId, ct);
        var ok = extras.All(l => colorOf.TryGetValue(l.ProductComponentMaterialId!.Value, out var colorId)
                                 && colorId == orderLines[l.OrderLineId].ProductVariant.ProductColorId);
        return ok ? null : Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.WorkOrderComponentInvalid);
    }

    /// <summary>Σ số kế hoạch của các lệnh cắt thường (không tính lệnh hủy, lệnh cắt bù) ≤ số đặt của dòng đơn.</summary>
    private async Task<Result<WorkOrderDto>?> CheckPlanCapAsync(SaveWorkOrderDto dto,
        IReadOnlyDictionary<int, OrderLine> orderLines, int? exceptWorkOrderId, CancellationToken ct)
    {
        var ids = orderLines.Keys.ToList();
        var planned = await _uow.Repository<WorkOrderLine>().Query()
            .Where(l => ids.Contains(l.OrderLineId) && l.ProductComponentMaterialId == null
                        && l.WorkOrder.Status != WorkOrderStatuses.Cancelled && l.WorkOrder.SourceRequestId == null
                        && (exceptWorkOrderId == null || l.WorkOrderId != exceptWorkOrderId))
            .GroupBy(l => l.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.PlannedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var over = dto.Lines.Where(l => l.ProductComponentMaterialId is null)
            .Select(l => (Line: orderLines[l.OrderLineId], Other: planned.GetValueOrDefault(l.OrderLineId), l.PlannedQty))
            .Where(x => x.Other + x.PlannedQty > x.Line.OrderedQty)
            .Select(x => $"{x.Line.ProductVariant.ProductColor.Color}-{x.Line.ProductVariant.SizeCode}: đặt {x.Line.OrderedQty}, đã lên lệnh {x.Other}, lệnh này {x.PlannedQty}")
            .ToList();
        return over.Count == 0
            ? null
            : Fail(ErrorCodes.CuttingPlanExceedsOrder, $"{WarehouseMessages.PlanExceedsOrder}: {string.Join("; ", over)}.");
    }

    private async Task<IReadOnlyList<FabricRequirementDto>> GetFabricsAsync(WorkOrder wo, CancellationToken ct)
    {
        var lines = wo.WorkOrderLines.Select(l => new FabricLine(l.OrderLine.ProductVariant.ProductColorId, l.ProductComponentMaterialId)).ToList();
        var infos = (await _planner.GetFabricsAsync(lines, ct)).ToDictionary(f => f.MaterialId);
        var issued = await _planner.GetIssuedAsync(wo.WorkOrderId, ct);
        var (received, returned) = await _planner.GetUsageAsync(wo.WorkOrderId, ct);
        var cut = await _planner.GetCutAsync(wo.CuttingTeamId, wo.OrderId, ct);

        // Vải đã cấp / trả cho lệnh nhưng không (còn) nằm trong định mức vẫn phải hiện.
        var extra = issued.Keys.Concat(received.Keys).Concat(returned.Keys).Where(id => !infos.ContainsKey(id)).Distinct().ToList();
        if (extra.Count > 0)
        {
            var more = await _uow.Repository<Material>().Query().AsNoTracking().Where(m => extra.Contains(m.MaterialId))
                .Select(m => new FabricInfo(m.MaterialId, m.MaterialCode, m.MaterialName, m.Unit, m.Color)).ToListAsync(ct);
            foreach (var m in more) infos[m.MaterialId] = m;
        }

        var result = new List<FabricRequirementDto>(infos.Count);
        foreach (var n in infos.Values.OrderBy(f => f.MaterialCode).ThenBy(f => f.MaterialColor))
        {
            var got = received.GetValueOrDefault(n.MaterialId);
            var back = returned.GetValueOrDefault(n.MaterialId);
            var available = await _stock.GetAvailableAsync(StockKey.Material(n.MaterialId), ct);
            var held = Math.Max(0, await _planner.GetTeamHoldingAsync(wo.CuttingTeamId, wo.OrderId, n.MaterialId, ct));
            var (cutQty, unmeasured) = cut.GetValueOrDefault(n.MaterialId);
            result.Add(new FabricRequirementDto(n.MaterialId, n.MaterialCode, n.MaterialName, n.Unit, n.MaterialColor,
                issued.GetValueOrDefault(n.MaterialId), available, got, back, Math.Max(0, got - back),
                held, cutQty, held - cutQty, unmeasured));
        }
        return result;
    }

    private async Task<Dictionary<(int OrderLineId, int? ProductComponentMaterialId), int>> HandedQtyAsync(int workOrderId, CancellationToken ct) =>
        await _uow.Repository<CutHandoverLine>().Query()
            .Where(l => l.CutHandover.WorkOrderId == workOrderId
                        && (l.CutHandover.Status == HandoverStatuses.Pending || l.CutHandover.Status == HandoverStatuses.Confirmed))
            .GroupBy(l => new { l.OrderLineId, l.ProductComponentMaterialId })
            .Select(g => new { g.Key, Qty = g.Sum(l => l.HandedQty) })
            .ToDictionaryAsync(x => (x.Key.OrderLineId, x.Key.ProductComponentMaterialId), x => x.Qty, ct);

    private static void Apply(WorkOrder wo, SaveWorkOrderDto dto)
    {
        wo.OrderId = dto.OrderId;
        wo.ProductId = dto.ProductId;
        wo.CuttingTeamId = dto.CuttingTeamId;
        wo.DueDate = dto.DueDate;
        wo.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        wo.FabricTolerancePercent = dto.FabricTolerancePercent;
        wo.WorkOrderLines = dto.Lines.Select(l => new WorkOrderLine
        {
            OrderLineId = l.OrderLineId,
            ProductComponentMaterialId = l.ProductComponentMaterialId,
            PlannedQty = l.PlannedQty,
        }).ToList();
    }

    private Task<string?> OrderCodeAsync(int orderId, CancellationToken ct) =>
        _uow.Repository<CustomerOrder>().Query().AsNoTracking().Where(o => o.OrderId == orderId).Select(o => (string?)o.OrderCode)
            .FirstOrDefaultAsync(ct);

    private static string Format(decimal qty) => qty.ToString("#,0.###", CultureInfo.GetCultureInfo("vi-VN"));

    private static Result<WorkOrderDto> Fail(string code, string message) => Result<WorkOrderDto>.Failure(code, message);
}
