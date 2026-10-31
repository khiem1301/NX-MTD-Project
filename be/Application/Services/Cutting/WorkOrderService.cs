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

    public WorkOrderService(IUnitOfWork uow, IStockService stock, FabricPlanner planner, IDocumentCodeGenerator codes,
        INotificationService notifier, ILogger<WorkOrderService> logger)
    {
        _uow = uow; _stock = stock; _planner = planner; _codes = codes; _notifier = notifier; _logger = logger;
    }

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
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine).ThenInclude(o => o.ProductVariant)
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.ProductComponentMaterial!).ThenInclude(p => p.ProductComponent)
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.ProductComponentMaterial!).ThenInclude(p => p.Material)
            .FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound<WorkOrderDto>();
        if (cuttingTeamId is not null && wo.CuttingTeamId != cuttingTeamId) return WarehouseFailures.TeamMismatch<WorkOrderDto>();

        var cut = await _uow.Repository<CuttingLayLine>().Query().Where(l => l.CuttingLay.WorkOrderId == workOrderId)
            .GroupBy(l => new { l.OrderLineId, l.ProductComponentMaterialId }).Select(g => new { g.Key, Qty = g.Sum(l => l.ActualQty) })
            .ToDictionaryAsync(x => (x.Key.OrderLineId, x.Key.ProductComponentMaterialId), x => x.Qty, ct);
        var handed = await HandedQtyAsync(workOrderId, ct);
        var lines = wo.WorkOrderLines.OrderBy(l => l.OrderLine.ProductVariant.Color).ThenBy(l => l.OrderLine.ProductVariant.SizeCode)
            .ThenBy(l => l.ProductComponentMaterial == null ? 0 : l.ProductComponentMaterial.ProductComponentId)
            .Select(l => new WorkOrderLineDto(l.WorkOrderLineId, l.OrderLineId, l.OrderLine.ProductVariant.Color,
                l.OrderLine.ProductVariant.SizeCode, l.ProductComponentMaterialId, ComponentLabels.Of(l.ProductComponentMaterial) ?? string.Empty,
                l.PlannedQty, cut.GetValueOrDefault((l.OrderLineId, l.ProductComponentMaterialId)),
                handed.GetValueOrDefault((l.OrderLineId, l.ProductComponentMaterialId))))
            .ToList();

        return Result<WorkOrderDto>.Success(new WorkOrderDto(wo.WorkOrderId, wo.WorkOrderCode, wo.OrderId,
            wo.Order.OrderCode, wo.ProductId, wo.Product.ProductCode, wo.Product.ProductName, wo.CuttingTeamId,
            wo.CuttingTeam.TeamName, wo.SourceRequestId, wo.DueDate, wo.Status, wo.Note, wo.CreatedAt, lines,
            await GetFabricsAsync(wo, ct)));
    }

    public async Task<Result<IReadOnlyList<FabricRequirementDto>>> GetFabricRequirementsAsync(int workOrderId,
        CancellationToken ct)
    {
        var wo = await WorkOrders.AsNoTracking().Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine)
            .ThenInclude(o => o.ProductVariant).FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        return wo is null
            ? WarehouseFailures.NotFound<IReadOnlyList<FabricRequirementDto>>()
            : Result<IReadOnlyList<FabricRequirementDto>>.Success(await GetFabricsAsync(wo, ct));
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
                    l.OrderLine!.ProductVariant.Color, l.OrderLine.ProductVariant.SizeCode, l.ProductComponentMaterialId!.Value,
                    l.ProductComponentMaterial!.ProductComponent.ComponentName + " – " + l.ProductComponentMaterial.Material.MaterialName + (l.ProductComponentMaterial.Material.Color == "" ? "" : " " + l.ProductComponentMaterial.Material.Color), (int)l.Quantity)).ToList()))
            .ToListAsync(ct);
        return Result<IReadOnlyList<ReplacementRequestDto>>.Success(requests);
    }

    public async Task<Result<WorkOrderDto>> CreateReplacementAsync(CreateReplacementWorkOrderDto dto, CancellationToken ct)
    {
        var request = await _uow.Repository<Request>().Query().AsNoTracking().Include(r => r.RequestLines)
            .FirstOrDefaultAsync(r => r.RequestId == dto.RequestId && r.RequestTypeCode == RequestTypes.ReplacementCutting, ct);
        if (request is null || request.OrderId is null) return WarehouseFailures.NotFound<WorkOrderDto>();
        if (request.Status != RequestStatuses.Approved)
            return Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.ReplacementRequestNotApproved);
        var hasOrder = await WorkOrders.AnyAsync(w => w.SourceRequestId == request.RequestId && w.Status != WorkOrderStatuses.Cancelled, ct);
        if (hasOrder) return Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.ReplacementOrderExists);
        if (request.RequestLines.Count == 0 || request.RequestLines.Any(l => l.OrderLineId is null || l.ProductComponentMaterialId is null))
            return Fail(ErrorCodes.WarehouseInvalidRequest, WarehouseMessages.ReplacementLinesMissing);

        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking().FirstAsync(o => o.OrderId == request.OrderId, ct);
        if (OrderStatuses.Closed.Contains(order.Status)) return Fail(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);
        var teamOk = await _uow.Repository<Team>().Query().AnyAsync(t => t.TeamId == dto.CuttingTeamId
            && t.TeamType == TeamTypes.Cutting && t.Status == RecordStatus.Active, ct);
        if (!teamOk) return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.CuttingTeamInvalid);

        var lineIds = request.RequestLines.Select(l => l.OrderLineId!.Value).Distinct().ToList();
        var orderLines = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Include(l => l.ProductVariant)
            .Where(l => lineIds.Contains(l.OrderLineId) && l.OrderId == request.OrderId).ToDictionaryAsync(l => l.OrderLineId, ct);
        if (orderLines.Count != lineIds.Count) return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.OrderLineNotInOrder);
        var productIds = orderLines.Values.Select(l => l.ProductId).Distinct().ToList();
        if (productIds.Count != 1) return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ProductNotInOrder);

        // Vải ước tính theo kích thước chi tiết cần cắt bù và khổ vải (vw_RecutFabricNeed) phải đủ trong kho + vải tổ cắt đang giữ.
        var fabric = await CheckRecutFabricAsync(request.OrderId.Value, dto.CuttingTeamId,
            request.RequestLines.Select(l => (l.ProductComponentMaterialId!.Value, (int)l.Quantity)).ToList(), ct);
        if (fabric is not null) return fabric;

        var created = await _uow.InTransactionAsync(async () =>
        {
            var wo = new WorkOrder
            {
                WorkOrderCode = await _codes.NextAsync<WorkOrder>(DocumentPrefixes.WorkOrder, w => w.WorkOrderCode, ct),
                Status = WorkOrderStatuses.Open,
                OrderId = request.OrderId.Value,
                ProductId = productIds[0],
                CuttingTeamId = dto.CuttingTeamId,
                DueDate = dto.DueDate,
                SourceRequestId = request.RequestId,
                Note = string.IsNullOrWhiteSpace(dto.Note) ? $"Cắt bù theo {request.RequestCode}" : dto.Note.Trim(),
                WorkOrderLines = request.RequestLines.Select(l => new WorkOrderLine
                {
                    OrderLineId = l.OrderLineId!.Value, ProductComponentMaterialId = l.ProductComponentMaterialId, PlannedQty = (int)l.Quantity,
                }).ToList(),
            };
            await _uow.Repository<WorkOrder>().AddAsync(wo);
            await NotifyCreatedAsync(wo, order.OrderCode, ct);
            await _uow.SaveChangesAsync();
            return Result<int>.Success(wo.WorkOrderId);
        });
        _logger.LogInformation("Replacement work order {WorkOrderId} created from request {RequestId}", created.Data, dto.RequestId);
        return await GetByIdAsync(created.Data, null, ct);
    }

    public async Task<Result<WorkOrderDto>> UpdateAsync(int workOrderId, SaveWorkOrderDto dto, CancellationToken ct)
    {
        var wo = await WorkOrders.Include(w => w.WorkOrderLines).FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound<WorkOrderDto>();
        var editable = await EnsureEditableAsync(wo, ct);
        if (!editable.IsSuccess) return WarehouseFailures.From<WorkOrderDto>(editable);

        var check = await ValidateAsync(dto, workOrderId, ct);
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

    public async Task<Result> FinishCuttingAsync(int workOrderId, int cuttingTeamId, string? reason, CancellationToken ct)
    {
        var wo = await WorkOrders.Include(w => w.WorkOrderLines).Include(w => w.Order)
            .FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound();
        if (wo.CuttingTeamId != cuttingTeamId) return WarehouseFailures.TeamMismatch();
        if (wo.Status != WorkOrderStatuses.Cutting)
            return Result.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderNotFinishable);

        var handovers = await _uow.Repository<CutHandoverLine>().Query().AsNoTracking()
            .Where(l => l.CutHandover.WorkOrderId == workOrderId)
            .Select(l => new { l.CutHandover.Status, l.OrderLineId, l.ProductComponentMaterialId, l.ConfirmedQty })
            .ToListAsync(ct);
        if (handovers.Any(h => h.Status == HandoverStatuses.Pending))
            return Result.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderHasPendingHandoverToFinish);
        // Thứ tự theo swimlane: BTP vào kho → trả vải thừa → báo Quản đốc kèm vải thực dùng. Phiếu trả chờ kho thì chưa tổng kết được.
        if (await _uow.Repository<MaterialReturn>().Query().AnyAsync(r => r.Status == HandoverStatuses.Pending
                && r.MaterialIssue.WorkOrderId == workOrderId, ct))
            return Result.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderHasPendingReturn);

        var cut = (await _uow.Repository<CuttingLayLine>().Query().AsNoTracking()
            .Where(l => l.CuttingLay.WorkOrderId == workOrderId)
            .Select(l => new { l.OrderLineId, l.ProductComponentMaterialId, l.ActualQty }).ToListAsync(ct))
            .GroupBy(l => (l.OrderLineId, l.ProductComponentMaterialId)).ToDictionary(g => g.Key, g => g.Sum(l => l.ActualQty));
        var received = handovers.Where(h => h.Status == HandoverStatuses.Confirmed)
            .GroupBy(h => (h.OrderLineId, h.ProductComponentMaterialId)).ToDictionary(g => g.Key, g => g.Sum(h => h.ConfirmedQty ?? 0));

        var notHanded = cut.Where(c => c.Value > received.GetValueOrDefault(c.Key)).Sum(c => c.Value - received.GetValueOrDefault(c.Key));
        if (notHanded > 0)
            return Result.Failure(ErrorCodes.WarehouseInvalidStatus, string.Format(WarehouseMessages.WorkOrderCutNotHandedOver, notHanded));

        var planned = wo.WorkOrderLines.Sum(l => l.PlannedQty);
        var inWarehouse = received.Values.Sum();
        reason = reason?.Trim();
        if (inWarehouse < planned && string.IsNullOrEmpty(reason))
            return Result.Failure(ErrorCodes.WarehouseInvalidReference, string.Format(WarehouseMessages.WorkOrderFinishShortReasonRequired, inWarehouse, planned));

        wo.Status = WorkOrderStatuses.Completed;
        wo.CompletedAt = DateTime.Now;
        if (!string.IsNullOrEmpty(reason)) wo.Note = string.IsNullOrEmpty(wo.Note) ? $"Cắt thiếu: {reason}" : $"{wo.Note}\nCắt thiếu: {reason}";
        var shortText = inWarehouse < planned ? $" Cắt {inWarehouse}/{planned}: {reason}." : string.Empty;
        var fabrics = await GetFabricsAsync(await WorkOrders.AsNoTracking().Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine)
            .FirstAsync(w => w.WorkOrderId == workOrderId, ct), ct);
        await _notifier.NotifyRoleAsync(RoleCodes.Manager, new NotificationMessage(NotificationSources.Production, "Lệnh cắt đã cắt xong",
            $"Tổ cắt báo lệnh {wo.WorkOrderCode} (đơn {wo.Order.OrderCode}) cắt xong, BTP đã vào kho.{shortText}{FabricUsage(fabrics)}", WorkOrderLink), ct);
        await _uow.SaveChangesAsync();
        _logger.LogInformation("Work order {WorkOrderId} finished by cutting team: {InWarehouse}/{Planned}", workOrderId, inWarehouse, planned);
        return Result.Success(WarehouseMessages.WorkOrderFinishedCutting);
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
        CancellationToken ct)
    {
        var order = await _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == dto.OrderId, ct);
        if (order is null) return WarehouseFailures.NotFound<WorkOrderDto>();
        if (OrderStatuses.Closed.Contains(order.Status))
            return Fail(ErrorCodes.WarehouseOrderClosed, WarehouseMessages.OrderClosed);

        var teamOk = await _uow.Repository<Team>().Query().AnyAsync(t => t.TeamId == dto.CuttingTeamId
            && t.TeamType == TeamTypes.Cutting && t.Status == RecordStatus.Active, ct);
        if (!teamOk) return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.CuttingTeamInvalid);

        var ids = dto.Lines.Select(l => l.OrderLineId).ToList();
        var orderLines = await _uow.Repository<OrderLine>().Query().AsNoTracking().Include(l => l.ProductVariant)
            .Where(l => ids.Contains(l.OrderLineId) && l.OrderId == dto.OrderId && l.ProductId == dto.ProductId)
            .ToDictionaryAsync(l => l.OrderLineId, ct);
        if (orderLines.Count != ids.Count) return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ProductNotInOrder);

        return await CheckPlanCapAsync(dto, orderLines, exceptWorkOrderId, ct);
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

        var over = dto.Lines
            .Select(l => (Line: orderLines[l.OrderLineId], Other: planned.GetValueOrDefault(l.OrderLineId), l.PlannedQty))
            .Where(x => x.Other + x.PlannedQty > x.Line.OrderedQty)
            .Select(x => $"{x.Line.ProductVariant.Color}-{x.Line.ProductVariant.SizeCode}: đặt {x.Line.OrderedQty}, đã lên lệnh {x.Other}, lệnh này {x.PlannedQty}")
            .ToList();
        return over.Count == 0
            ? null
            : Fail(ErrorCodes.CuttingPlanExceedsOrder, $"{WarehouseMessages.PlanExceedsOrder}: {string.Join("; ", over)}.");
    }

    /// <summary>
    /// Lệnh cắt bù: ước tính số mét vải theo kích thước chi tiết (định mức) và khổ vải, so với vải khả dụng trong kho + vải tổ cắt
    /// đang giữ. Thiếu kích thước thì không ước tính được và cho qua. Lệnh cắt thường không kiểm vải ở đây (DB 29/10 bỏ lượng định mức).
    /// </summary>
    private async Task<Result<WorkOrderDto>?> CheckRecutFabricAsync(int orderId, int cuttingTeamId,
        IReadOnlyCollection<(int ProductComponentMaterialId, int Pieces)> lines, CancellationToken ct)
    {
        var ids = lines.Select(l => l.ProductComponentMaterialId).Distinct().ToArray();
        var norms = await _uow.Repository<ProductComponentMaterial>().Query().AsNoTracking()
            .Where(n => ids.Contains(n.ProductComponentMaterialId))
            .Select(n => new
            {
                n.ProductComponentMaterialId, n.MaterialId, n.LengthCm, n.WidthCm,
                n.Material.MaterialCode, n.Material.MaterialName, n.Material.Color, n.Material.Unit, FabricWidthCm = n.Material.WidthCm,
            })
            .ToDictionaryAsync(n => n.ProductComponentMaterialId, ct);

        var need = new Dictionary<int, (decimal Meters, string Label)>();
        foreach (var (pcmId, pieces) in lines)
        {
            if (!norms.TryGetValue(pcmId, out var n) || n.Unit != "m") continue;
            var meters = RecutFabricEstimator.NeedMeters(pieces, n.LengthCm, n.WidthCm, n.FabricWidthCm);
            if (meters is null) continue;
            var label = $"{n.MaterialCode} {n.MaterialName}{(n.Color == "" ? "" : $" ({n.Color})")}";
            need[n.MaterialId] = need.TryGetValue(n.MaterialId, out var cur) ? (cur.Meters + meters.Value, label) : (meters.Value, label);
        }

        var shortages = new List<string>();
        foreach (var (materialId, (meters, label)) in need)
        {
            var warehouse = await _stock.GetAvailableAsync(StockKey.Material(materialId), ct);
            var holding = Math.Max(0, await _planner.GetTeamHoldingAsync(cuttingTeamId, orderId, materialId, ct));
            var available = warehouse + holding;
            if (available < meters)
                shortages.Add($"{label}: cần khoảng {Format(meters)} m, có {Format(available)} (kho {Format(warehouse)}, tổ đang giữ {Format(holding)})");
        }
        return shortages.Count == 0
            ? null
            : Fail(ErrorCodes.CuttingFabricShortage, $"{WarehouseMessages.FabricShortage}: {string.Join("; ", shortages)}.");
    }

    private async Task<IReadOnlyList<FabricRequirementDto>> GetFabricsAsync(WorkOrder wo, CancellationToken ct)
    {
        var lines = wo.WorkOrderLines.Select(l => new FabricLine(l.OrderLine.ProductVariantId, l.ProductComponentMaterialId)).ToList();
        var infos = (await _planner.GetFabricsAsync(lines, ct)).ToDictionary(f => f.MaterialId);
        var issued = await _planner.GetIssuedAsync(wo.WorkOrderId, ct);
        var (received, returned) = await _planner.GetUsageAsync(wo.WorkOrderId, ct);

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
            result.Add(new FabricRequirementDto(n.MaterialId, n.MaterialCode, n.MaterialName, n.Unit, n.MaterialColor,
                issued.GetValueOrDefault(n.MaterialId), available, got, back, Math.Max(0, got - back)));
        }
        return result;
    }

    /// <summary>Vải đã nhận, đã trả, thực dùng của lệnh (không so định mức); hiện trong thông báo cho Quản đốc.</summary>
    private static string FabricUsage(IEnumerable<FabricRequirementDto> fabrics)
    {
        var rows = fabrics.Where(f => f.ReceivedQty > 0).Select(f =>
            $"{f.MaterialCode}: nhận {Format(f.ReceivedQty)}, trả {Format(f.ReturnedQty)}, thực dùng {Format(f.UsedQty)} {f.Unit}").ToList();
        return rows.Count == 0 ? string.Empty : $" Vải: {string.Join("; ", rows)}.";
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
        wo.WorkOrderLines = dto.Lines.Select(l => new WorkOrderLine
        {
            OrderLineId = l.OrderLineId,
            PlannedQty = l.PlannedQty,
        }).ToList();
    }

    private Task<string?> OrderCodeAsync(int orderId, CancellationToken ct) =>
        _uow.Repository<CustomerOrder>().Query().AsNoTracking().Where(o => o.OrderId == orderId).Select(o => (string?)o.OrderCode)
            .FirstOrDefaultAsync(ct);

    private static string Format(decimal qty) => qty.ToString("#,0.###", CultureInfo.GetCultureInfo("vi-VN"));

    private static Result<WorkOrderDto> Fail(string code, string message) => Result<WorkOrderDto>.Failure(code, message);
}
