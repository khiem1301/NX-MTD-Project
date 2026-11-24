using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed partial class HomeDashboardService
{
    /// <summary>Giám đốc: đơn giá, bảng lương chờ duyệt; doanh thu và chi phí tháng này so tháng trước; tiến độ đơn đang chạy.</summary>
    public async Task<Result<AdminHomeDto>> GetAdminAsync(CancellationToken ct)
    {
        var pieceRates = await _uow.Repository<PieceRate>().Query().AsNoTracking()
            .CountAsync(r => r.Status == PieceRateStatuses.PendingApproval, ct);
        var payrolls = await _uow.Repository<Payroll>().Query().AsNoTracking()
            .CountAsync(p => p.Status == PayrollStatus.PendingApproval, ct);
        var (thisMonth, lastMonth) = Months();
        var revenue = new HomeMoneyDto(await RevenueOfAsync(thisMonth, ct), await RevenueOfAsync(lastMonth, ct));
        var cost = new HomeMoneyDto(await CostOfAsync(thisMonth, ct), await CostOfAsync(lastMonth, ct));
        var running = (await OpenOrdersAsync(ct)).Where(o => IsRunningStatus(o.Status)).ToList();
        return Result<AdminHomeDto>.Success(new AdminHomeDto(pieceRates, payrolls, running.Count, revenue, cost, running));
    }

    /// <summary>Doanh thu một tháng, cùng nguồn với màn Doanh thu (vw_MonthlyRevenue). Lỗi nguồn thì 0 để trang chủ vẫn mở.</summary>
    private async Task<decimal> RevenueOfAsync(string month, CancellationToken ct)
    {
        try
        {
            return (await _finance.GetRevenueRowsAsync(month, null, null, ct)).Sum(r => r.Revenue ?? 0);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Tổng chi phí một tháng như màn Tổng hợp chi phí; null khi tháng đó chưa nhập chi phí.</summary>
    private async Task<decimal?> CostOfAsync(string month, CancellationToken ct)
    {
        var result = await _costs.GetAsync(month, ct);
        return result.IsSuccess ? result.Data!.GrandTotal : null;
    }
}
