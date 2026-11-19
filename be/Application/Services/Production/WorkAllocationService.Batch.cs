using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-126: công nhân xác nhận nhiều phiếu giao việc một lần. Mỗi phiếu qua đúng các kiểm tra của xác nhận từng phiếu
/// (của mình, đang chờ, số trong khoảng, lệch số phải có lý do, khóa theo tổ × dòng đơn); một phiếu lỗi thì không lưu phiếu nào.
/// Tổ trưởng / tổ phó nhận một thông báo gộp thay vì một thông báo mỗi phiếu.
/// </summary>
public sealed partial class WorkAllocationService
{
    public Task<Result> ConfirmBatchByWorkerAsync(ConfirmWorkAllocationBatchDto dto, int workerId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var confirmed = new List<WorkAllocation>();
            foreach (var item in dto.Items)
            {
                var (allocation, failure) = await LoadPendingAsync(item.WorkAllocationId, m => m.WorkerId == workerId, ct);
                if (allocation is null) return LineFailure($"#{item.WorkAllocationId}", failure!);
                var result = await ApplyConfirmAsync(allocation, item.ConfirmedQty, item.DiscrepancyReason,
                    ConfirmModes.Worker, null, ct);
                if (!result.IsSuccess) return LineFailure(allocation.Operation.OperationName, result);
                AuditDecision(allocation);
                confirmed.Add(allocation);
            }
            await NotifyBatchAsync(confirmed, workerId, ct);
            await _uow.SaveChangesAsync();
            return Result.Success(string.Format(WarehouseMessages.PieceMovementsBatchConfirmed, confirmed.Count));
        });

    private async Task NotifyBatchAsync(IReadOnlyList<WorkAllocation> confirmed, int workerId, CancellationToken ct)
    {
        var workerName = await _uow.Repository<Employee>().Query().AsNoTracking()
            .Where(e => e.EmployeeId == workerId).Select(e => e.FullName).FirstOrDefaultAsync(ct);
        foreach (var team in confirmed.GroupBy(a => a.SewingTeamId))
        {
            _notifier.NotifyEmployees(await LeaderIdsAsync(team.Key, ct), new NotificationMessage(NotificationSources.Production,
                WarehouseMessages.BatchConfirmNotifyTitle,
                string.Format(WarehouseMessages.BatchConfirmNotifyBody, workerName, team.Count()), null));
        }
    }

    private static Result LineFailure(string label, Result failure) =>
        Result.Failure(failure.ErrorCode ?? ErrorCodes.WarehouseInvalidReference,
            string.Format(WarehouseMessages.BatchConfirmLineFailed, label, failure.Message), failure.Kind);
}
