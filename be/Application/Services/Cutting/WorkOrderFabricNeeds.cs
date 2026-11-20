using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Số mét vải hệ thống tính cho lệnh cắt (ghép bìa, DEV-ALL-88) và phần còn xin được theo trần của lệnh (DEV-ALL-92).</summary>
public sealed class WorkOrderFabricNeeds : IWorkOrderFabricNeeds
{
    private static readonly string[] ClosedRequestStatuses = { RequestStatuses.Rejected, RequestStatuses.Cancelled, RequestStatuses.NotCompleted };

    private readonly IUnitOfWork _uow;
    private readonly WorkOrderFabricChecker _checker;

    public WorkOrderFabricNeeds(IUnitOfWork uow, WorkOrderFabricChecker checker)
    {
        _uow = uow; _checker = checker;
    }

    public Task<WorkOrder?> LoadAsync(int workOrderId, CancellationToken ct) =>
        _uow.Repository<WorkOrder>().Query().AsNoTracking()
            .Include(w => w.WorkOrderLines).ThenInclude(l => l.OrderLine).ThenInclude(o => o.ProductVariant).ThenInclude(v => v.ProductColor)
            .FirstOrDefaultAsync(w => w.WorkOrderId == workOrderId, ct);

    /// <summary>Vải hệ thống tính cho các dòng lệnh (đã lọc); NeedMeters null = chưa tính được.</summary>
    public async Task<IReadOnlyList<WorkOrderFabricCheckDto>> NeedsAsync(WorkOrder wo, IEnumerable<WorkOrderLine> lines, CancellationToken ct)
    {
        var cutLines = lines.Select(l => new CutLine(l.OrderLine.ProductVariantId, l.PlannedQty,
            $"{l.OrderLine.ProductVariant.ProductColor.Color} - {l.OrderLine.ProductVariant.SizeCode}", l.ProductComponentMaterialId)).ToList();
        if (cutLines.Count == 0) return [];
        var checks = await _checker.CheckAsync(wo.OrderId, wo.CuttingTeamId, cutLines, ct);
        return checks.Where(c => c.MaterialId > 0).ToList();
    }

    /// <summary>Trần = cần × (1 + tỉ lệ sai lệch), làm tròn xuống 2 số lẻ như số mét vải nhập được.</summary>
    public static decimal Cap(decimal needQty, decimal tolerancePercent) =>
        decimal.Floor(needQty * (1 + tolerancePercent / 100m) * 100m) / 100m;

    public async Task<IReadOnlyList<WorkOrderMaterialAllowanceDto>> AllowanceAsync(WorkOrder wo, int? exceptRequestId, CancellationToken ct) =>
        await AllowanceFromNeedsAsync(wo, await NeedsAsync(wo, wo.WorkOrderLines, ct), exceptRequestId, ct);

    /// <summary>Trần, đã xin (đơn xin NVL của lệnh, trừ đơn bị từ chối / hủy và đơn đang sửa) và còn xin được của từng vải.</summary>
    public async Task<IReadOnlyList<WorkOrderMaterialAllowanceDto>> AllowanceFromNeedsAsync(WorkOrder wo,
        IReadOnlyList<WorkOrderFabricCheckDto> needs, int? exceptRequestId, CancellationToken ct)
    {
        var requested = await _uow.Repository<RequestLine>().Query().AsNoTracking()
            .Where(l => l.Request.WorkOrderId == wo.WorkOrderId && l.Request.RequestTypeCode == RequestTypes.MaterialRequisition
                        && !ClosedRequestStatuses.Contains(l.Request.Status) && l.MaterialId != null
                        && (exceptRequestId == null || l.RequestId != exceptRequestId))
            .GroupBy(l => l.MaterialId!.Value).Select(g => new { g.Key, Qty = g.Sum(l => l.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty, ct);
        var ids = needs.Select(n => n.MaterialId).ToList();
        var units = await _uow.Repository<Material>().Query().AsNoTracking().Where(m => ids.Contains(m.MaterialId))
            .ToDictionaryAsync(m => m.MaterialId, m => m.Unit, ct);

        return needs.Select(n =>
        {
            var cap = n.NeedMeters is { } need ? Cap(need, wo.FabricTolerancePercent) : (decimal?)null;
            var done = requested.GetValueOrDefault(n.MaterialId);
            return new WorkOrderMaterialAllowanceDto(n.MaterialId, n.MaterialCode, n.MaterialName, n.MaterialColor,
                units.GetValueOrDefault(n.MaterialId, string.Empty), n.NeedMeters, cap, done, cap is null ? null : Math.Max(0, cap.Value - done));
        }).ToList();
    }
}
