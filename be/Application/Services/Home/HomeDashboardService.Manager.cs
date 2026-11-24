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
        var requests = await InboxAsync(ct);
        var absent = await _uow.Repository<VwTeamActingLeader>().Query().AsNoTracking()
            .Where(t => t.IsActingLeader == true).Select(t => t.TeamCode).Distinct().OrderBy(c => c).ToListAsync(ct);
        return Result<ManagerHomeDto>.Success(new ManagerHomeDto(
            receipts, requests.TotalItems, absent, await OpenOrdersAsync(ct), requests.Items));
    }

    /// <summary>Hộp đơn từ chờ duyệt. Lỗi thì danh sách rỗng, các khối khác của trang chủ vẫn hiện.</summary>
    private async Task<PagedResult<RequestListItemDto>> InboxAsync(CancellationToken ct)
    {
        try
        {
            var inbox = await _requests.ListInboxAsync(
                new RequestQuery { Page = 1, PageSize = LatestCount, Status = CodeText.ToPascal(RequestStatuses.PendingApproval) }, ct);
            return inbox.IsSuccess ? inbox.Data! : EmptyInbox();
        }
        catch (Exception)
        {
            return EmptyInbox();
        }
    }

    private static PagedResult<RequestListItemDto> EmptyInbox() => new([], 1, LatestCount, 0);
}
