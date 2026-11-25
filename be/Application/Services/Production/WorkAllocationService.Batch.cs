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

    /// <summary>DEV-ALL-182: tổ trưởng xác nhận hộ cả công nhân - mọi phiếu đang chờ của công nhân trong tổ, mỗi phiếu đủ số, cùng một lý do.</summary>
    public Task<Result> ConfirmWorkerOnBehalfAsync(int workerId, ConfirmWorkerOnBehalfDto dto, int teamId, CancellationToken ct) =>
        _uow.InTransactionAsync(async () =>
        {
            var ids = await Allocations.Where(m => m.WorkerId == workerId && m.SewingTeamId == teamId && m.Status == HandoverStatuses.Pending)
                .OrderBy(m => m.WorkAllocationId).Select(m => m.WorkAllocationId).ToListAsync(ct);
            if (ids.Count == 0) return WarehouseFailures.NotFound();

            var reason = dto.OnBehalfReason.Trim();
            var confirmed = 0;
            foreach (var id in ids)
            {
                var (allocation, failure) = await LoadPendingAsync(id, m => m.SewingTeamId == teamId, ct);
                if (allocation is null) return LineFailure($"#{id}", failure!);
                var result = await ApplyConfirmAsync(allocation, allocation.Quantity, null, ConfirmModes.OnBehalf, reason, ct);
                if (!result.IsSuccess) return LineFailure(allocation.Operation.OperationName, result);
                AuditDecision(allocation);
                confirmed++;
            }
            await _uow.SaveChangesAsync();
            return Result.Success(string.Format(WarehouseMessages.PieceMovementsBatchConfirmed, confirmed));
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
