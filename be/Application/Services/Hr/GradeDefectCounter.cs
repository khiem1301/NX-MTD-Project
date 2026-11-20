using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>Số phiếu lỗi gần đây ghi vào đề xuất bậc (<c>GradeEvaluation.RecentDefectCount</c>), dùng chung cho đề xuất tay và đề xuất từ AI.</summary>
internal static class GradeDefectCounter
{
    public const int WindowDays = 30;

    /// <summary>Số phiếu lỗi (Rework + Scrap, không tính đã hủy) do công nhân gây ra trong 30 ngày gần nhất.</summary>
    public static async Task<Dictionary<int, int>> CountRecentAsync(IUnitOfWork uow, DateOnly today,
        IReadOnlyCollection<int> workerIds, CancellationToken ct)
    {
        var from = today.AddDays(-WindowDays);
        return await uow.Repository<DefectRecord>().Query()
            .Where(d => workerIds.Contains(d.ResponsibleWorkerId) && d.Status != DefectStatuses.Cancelled && d.WorkDate >= from)
            .GroupBy(d => d.ResponsibleWorkerId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }
}
