using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// Công đoạn phải có đơn giá được duyệt (hiệu lực đến ngày giao) thì mới giao được cho công nhân (DEV-ALL-73).
/// Cùng điều kiện với lúc nhập sản lượng (MSG18), kiểm sớm ở bước giao để khỏi giao việc mà không nhập được sản lượng.
/// </summary>
internal static class AllocationRateCheck
{
    /// <summary>Thông báo lỗi của công đoạn đầu tiên (theo thứ tự gửi lên) chưa có đơn giá duyệt; null nếu đủ đơn giá.</summary>
    public static async Task<string?> FindMissingAsync(IUnitOfWork uow, IReadOnlyCollection<int> operationIds, DateOnly date,
        CancellationToken ct)
    {
        if (operationIds.Count == 0) return null;
        var withRate = await uow.Repository<PieceRate>().Query().AsNoTracking()
            .Where(r => operationIds.Contains(r.OperationId) && r.Status == PieceRateStatuses.Approved && r.EffectiveDate <= date)
            .Select(r => r.OperationId).Distinct().ToListAsync(ct);
        var missing = operationIds.Distinct().Where(id => !withRate.Contains(id)).ToList();
        if (missing.Count == 0) return null;

        var codes = await uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => missing.Contains(o.OperationId)).OrderBy(o => o.StationSequence).Select(o => o.OperationCode).ToListAsync(ct);
        return string.Format(Messages.AllocationPieceRateMissing, string.Join(", ", codes));
    }
}
