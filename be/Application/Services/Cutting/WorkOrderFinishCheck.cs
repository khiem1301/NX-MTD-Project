using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

internal sealed record FinishCheck(Result? Failure, int InWarehouse = 0, int Planned = 0);

/// <summary>Điều kiện kết thúc lệnh cắt, dùng chung cho Quản đốc đóng lệnh cắt thiếu và việc tự đóng khi đơn dừng.</summary>
internal static class WorkOrderFinishCheck
{
    /// <summary>
    /// Chỉ kết thúc khi mọi phiếu bàn giao và phiếu trả vải đã xử lý, phần đã cắt đã vào kho BTP hết; cắt thiếu phải có lý do.
    /// Trả về số BTP đã vào kho và số kế hoạch.
    /// </summary>
    public static async Task<FinishCheck> RunAsync(IUnitOfWork uow, WorkOrder wo, string? reason, CancellationToken ct)
    {
        var workOrderId = wo.WorkOrderId;
        var handovers = await uow.Repository<CutHandoverLine>().Query().AsNoTracking()
            .Where(l => l.CutHandover.WorkOrderId == workOrderId)
            .Select(l => new { l.CutHandover.Status, l.OrderLineId, l.ProductComponentMaterialId, l.ConfirmedQty })
            .ToListAsync(ct);
        if (handovers.Any(h => h.Status == HandoverStatuses.Pending))
            return new FinishCheck(Result.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderHasPendingHandoverToFinish));
        // Thứ tự theo swimlane: BTP vào kho → trả vải thừa → báo Quản đốc kèm vải thực dùng. Phiếu trả chờ kho thì chưa tổng kết được.
        if (await uow.Repository<MaterialReturn>().Query().AnyAsync(r => r.Status == HandoverStatuses.Pending
                && r.WorkOrderId == workOrderId, ct))
            return new FinishCheck(Result.Failure(ErrorCodes.WarehouseInvalidStatus, WarehouseMessages.WorkOrderHasPendingReturn));

        var cut = (await uow.Repository<CuttingLayLine>().Query().AsNoTracking()
            .Where(l => l.CuttingLay.WorkOrderId == workOrderId)
            .Select(l => new { l.OrderLineId, l.ProductComponentMaterialId, l.ActualQty }).ToListAsync(ct))
            .GroupBy(l => (l.OrderLineId, l.ProductComponentMaterialId)).ToDictionary(g => g.Key, g => g.Sum(l => l.ActualQty));
        var received = handovers.Where(h => h.Status == HandoverStatuses.Confirmed)
            .GroupBy(h => (h.OrderLineId, h.ProductComponentMaterialId)).ToDictionary(g => g.Key, g => g.Sum(h => h.ConfirmedQty ?? 0));

        var notHanded = cut.Where(c => c.Value > received.GetValueOrDefault(c.Key)).Sum(c => c.Value - received.GetValueOrDefault(c.Key));
        if (notHanded > 0)
            return new FinishCheck(Result.Failure(ErrorCodes.WarehouseInvalidStatus, string.Format(WarehouseMessages.WorkOrderCutNotHandedOver, notHanded)));

        var planned = wo.WorkOrderLines.Sum(l => l.PlannedQty);
        var inWarehouse = received.Values.Sum();
        if (inWarehouse < planned && string.IsNullOrEmpty(reason))
            return new FinishCheck(Result.Failure(ErrorCodes.WarehouseInvalidReference, string.Format(WarehouseMessages.WorkOrderFinishShortReasonRequired, inWarehouse, planned)));
        return new FinishCheck(null, inWarehouse, planned);
    }
}
