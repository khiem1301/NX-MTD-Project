using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed partial class HomeDashboardService
{
    /// <summary>Quản đốc: phiếu nhập NVL chờ xác nhận, đơn từ chờ duyệt, tổ chưa có tổ trưởng điểm danh, tiến độ đơn còn mở.</summary>
    public async Task<Result<ManagerHomeDto>> GetManagerAsync(CancellationToken ct)
    {
        var receipts = await _uow.Repository<MaterialReceipt>().Query().AsNoTracking()
            .CountAsync(r => r.Status == ReceiptStatuses.Pending, ct);
        var inbox = await _requests.ListInboxAsync(
            new RequestQuery { Page = 1, PageSize = LatestCount, Status = CodeText.ToPascal(RequestStatuses.PendingApproval) }, ct);
        var requests = inbox.IsSuccess ? inbox.Data! : new PagedResult<RequestListItemDto>([], 1, LatestCount, 0);
        var absent = await _uow.Repository<VwTeamActingLeader>().Query().AsNoTracking()
            .Where(t => t.IsActingLeader == true).Select(t => t.TeamCode).Distinct().OrderBy(c => c).ToListAsync(ct);
        return Result<ManagerHomeDto>.Success(new ManagerHomeDto(
            receipts, requests.TotalItems, absent, await OpenOrdersAsync(ct), requests.Items));
    }
}
