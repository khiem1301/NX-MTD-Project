using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>UC19 (tổ cắt): Quản đốc lập lệnh cắt theo dòng đơn; thiếu vải thì chặn và nêu rõ vải thiếu.</summary>
public sealed class WorkOrderService : IWorkOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IStockService _stock;
    private readonly FabricPlanner _planner;
    private readonly IDocumentCodeGenerator _codes;
    private readonly ILogger<WorkOrderService> _logger;

    public WorkOrderService(IUnitOfWork uow, IStockService stock, FabricPlanner planner, IDocumentCodeGenerator codes,
        ILogger<WorkOrderService> logger)
    {
        _uow = uow; _stock = stock; _planner = planner; _codes = codes; _logger = logger;
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
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine)
            .FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
        if (wo is null) return WarehouseFailures.NotFound<WorkOrderDto>();
        if (cuttingTeamId is not null && wo.CuttingTeamId != cuttingTeamId) return WarehouseFailures.TeamMismatch<WorkOrderDto>();

        var cut = await _uow.Repository<CuttingLayLine>().Query().Where(l => l.CuttingLay.WorkOrderId == workOrderId)
            .GroupBy(l => l.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.ActualQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var handed = await HandedQtyAsync(workOrderId, ct);
        var lines = wo.WorkOrderLines.OrderBy(l => l.OrderLine.Color).ThenBy(l => l.OrderLine.SizeCode)
            .Select(l => new WorkOrderLineDto(l.WorkOrderLineId, l.OrderLineId, l.OrderLine.Color, l.OrderLine.SizeCode,
                l.PanelName, l.PlannedQty, cut.GetValueOrDefault(l.OrderLineId), handed.GetValueOrDefault(l.OrderLineId)))
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
            .FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);
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
            await _uow.SaveChangesAsync();
            return Result<int>.Success(wo.WorkOrderId);
        });
        _logger.LogInformation("Work order {WorkOrderId} created for order {OrderId}", created.Data, dto.OrderId);
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
        await _uow.SaveChangesAsync();
        return Result.Success(WarehouseMessages.WorkOrderCancelled);
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
        var orderLines = await _uow.Repository<OrderLine>().Query().AsNoTracking()
            .Where(l => ids.Contains(l.OrderLineId) && l.OrderId == dto.OrderId && l.ProductId == dto.ProductId)
            .ToDictionaryAsync(l => l.OrderLineId, ct);
        if (orderLines.Count != ids.Count) return Fail(ErrorCodes.WarehouseInvalidReference, WarehouseMessages.ProductNotInOrder);

        return await CheckPlanCapAsync(dto, orderLines, exceptWorkOrderId, ct)
               ?? await CheckFabricAsync(dto, orderLines, ct);
    }

    /// <summary>Σ số kế hoạch của các lệnh cắt thường (không tính lệnh hủy, lệnh cắt bù) ≤ số đặt của dòng đơn.</summary>
    private async Task<Result<WorkOrderDto>?> CheckPlanCapAsync(SaveWorkOrderDto dto,
        IReadOnlyDictionary<int, OrderLine> orderLines, int? exceptWorkOrderId, CancellationToken ct)
    {
        var ids = orderLines.Keys.ToList();
        var planned = await _uow.Repository<WorkOrderLine>().Query()
            .Where(l => ids.Contains(l.OrderLineId) && l.PanelName == string.Empty
                        && l.WorkOrder.Status != WorkOrderStatuses.Cancelled && l.WorkOrder.SourceRequestId == null
                        && (exceptWorkOrderId == null || l.WorkOrderId != exceptWorkOrderId))
            .GroupBy(l => l.OrderLineId).Select(g => new { g.Key, Qty = g.Sum(l => l.PlannedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

        var over = dto.Lines
            .Select(l => (Line: orderLines[l.OrderLineId], Other: planned.GetValueOrDefault(l.OrderLineId), l.PlannedQty))
            .Where(x => x.Other + x.PlannedQty > x.Line.OrderedQty)
            .Select(x => $"{x.Line.Color}-{x.Line.SizeCode}: đặt {x.Line.OrderedQty}, đã lên lệnh {x.Other}, lệnh này {x.PlannedQty}")
            .ToList();
        return over.Count == 0
            ? null
            : Fail(ErrorCodes.CuttingPlanExceedsOrder, $"{WarehouseMessages.PlanExceedsOrder}: {string.Join("; ", over)}.");
    }

    /// <summary>Vải cần (định mức) ≤ khả dụng trong kho + vải tổ cắt đang giữ của đơn; thiếu thì chặn.</summary>
    private async Task<Result<WorkOrderDto>?> CheckFabricAsync(SaveWorkOrderDto dto,
        IReadOnlyDictionary<int, OrderLine> orderLines, CancellationToken ct)
    {
        var demands = dto.Lines.Select(l => orderLines[l.OrderLineId])
            .Zip(dto.Lines, (ol, l) => new PieceDemand(ol.ProductId, ol.Color, ol.SizeCode, string.Empty, l.PlannedQty));
        var needs = await _planner.GetNeedsAsync(dto.OrderId, demands.ToList(), ct);
        if (needs.Count == 0) return Fail(ErrorCodes.CuttingFabricShortage, WarehouseMessages.NoFabricNorm);

        var shortages = new List<string>();
        foreach (var need in needs)
        {
            var available = await _stock.GetAvailableAsync(StockKey.Material(need.MaterialId, need.MaterialColor), ct)
                            + await _planner.GetTeamHoldingAsync(dto.CuttingTeamId, dto.OrderId, need.MaterialId,
                                need.MaterialColor, ct);
            if (available < need.RequiredQty)
                shortages.Add($"{need.MaterialCode} {need.MaterialName} ({need.MaterialColor}): cần {Format(need.RequiredQty)} {need.Unit}, " +
                              $"có {Format(available)}, thiếu {Format(need.RequiredQty - available)}");
        }
        return shortages.Count == 0
            ? null
            : Fail(ErrorCodes.CuttingFabricShortage, $"{WarehouseMessages.FabricShortage}: {string.Join("; ", shortages)}.");
    }

    private async Task<IReadOnlyList<FabricRequirementDto>> GetFabricsAsync(WorkOrder wo, CancellationToken ct)
    {
        var demands = wo.WorkOrderLines.Select(l => new PieceDemand(l.OrderLine.ProductId, l.OrderLine.Color,
            l.OrderLine.SizeCode, l.PanelName, l.PlannedQty)).ToList();
        var needs = await _planner.GetNeedsAsync(wo.OrderId, demands, ct);
        var issued = await _planner.GetIssuedAsync(wo.WorkOrderId, ct);

        var result = new List<FabricRequirementDto>(needs.Count);
        foreach (var n in needs)
        {
            var done = issued.GetValueOrDefault((n.MaterialId, n.MaterialColor));
            var available = await _stock.GetAvailableAsync(StockKey.Material(n.MaterialId, n.MaterialColor), ct);
            result.Add(new FabricRequirementDto(n.OrderMaterialId, n.MaterialId, n.MaterialCode, n.MaterialName, n.Unit,
                n.MaterialColor, n.RequiredQty, done, Math.Max(0, n.RequiredQty - done), available));
        }
        return result;
    }

    private async Task<Dictionary<int, int>> HandedQtyAsync(int workOrderId, CancellationToken ct) =>
        await _uow.Repository<CutHandoverLine>().Query()
            .Where(l => l.CutHandover.WorkOrderId == workOrderId
                        && (l.CutHandover.Status == HandoverStatuses.Pending || l.CutHandover.Status == HandoverStatuses.Confirmed))
            .GroupBy(l => l.OrderLineId)
            .Select(g => new { g.Key, Qty = g.Sum(l => l.HandedQty) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);

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
            PanelName = string.Empty,
            PlannedQty = l.PlannedQty,
        }).ToList();
    }

    private static string Format(decimal qty) => qty.ToString("#,0.###", CultureInfo.GetCultureInfo("vi-VN"));

    private static Result<WorkOrderDto> Fail(string code, string message) => Result<WorkOrderDto>.Failure(code, message);
}
