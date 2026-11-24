using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed partial class HomeDashboardService
{
    /// <summary>Số ticket tối đa hiện trên trang chủ cơ điện.</summary>
    private const int MaxTickets = 20;

    /// <summary>Cơ điện: các đơn từ do mình tạo còn mở (chờ duyệt / chờ xử lý / đã duyệt), hiện như ticket.</summary>
    public async Task<Result<ElectricianHomeDto>> GetElectricianAsync(CancellationToken ct)
    {
        try
        {
            var open = new[] { RequestStatuses.PendingApproval, RequestStatuses.PendingProcessing, RequestStatuses.Approved };
            var rows = await _uow.Repository<Request>().Query().AsNoTracking()
                .Where(r => r.CreatedBy == _user.EmployeeId && open.Contains(r.Status))
                .OrderByDescending(r => r.RequestId).Take(MaxTickets)
                .Select(r => new { r.RequestId, r.RequestCode, r.RequestTypeCode, r.Status, r.Title, r.CreatedAt }).ToListAsync(ct);
            var tickets = rows.Select(r => new HomeTicketDto(r.RequestId, r.RequestCode, CodeText.ToPascal(r.RequestTypeCode),
                CodeText.ToPascal(r.Status), r.Title, r.CreatedAt)).ToList();
            return Result<ElectricianHomeDto>.Success(new ElectricianHomeDto(tickets));
        }
        catch (Exception)
        {
            return Result<ElectricianHomeDto>.Success(new ElectricianHomeDto([]));
        }
    }
}
