using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed partial class HomeDashboardService
{
    /// <summary>Kế toán: chấm công, đơn giá, đơn mua, đơn chưa chốt giá trị và tình trạng chi phí / lương của tháng này.</summary>
    public async Task<Result<AccountantHomeDto>> GetAccountantAsync(CancellationToken ct)
    {
        var (month, _) = Months();
        var today = _clock.Today;
        var missingPunch = await MissingPunchAsync(today, ct);
        var (missing, rejected) = await PieceRateGapsAsync(ct);
        var (notSet, orders) = await OrdersNotSetAsync(ct);
        var (unpaid, purchases) = await PurchasesUnpaidAsync(ct);
        var (costEntered, payroll) = await MonthStatusAsync(month, ct);
        return Result<AccountantHomeDto>.Success(new AccountantHomeDto(
            missingPunch, missing, rejected, unpaid, notSet, costEntered, payroll,
            orders, purchases));
    }

    /// <summary>Số người thiếu chấm công. Lỗi bảng công thì 0.</summary>
    private async Task<int> MissingPunchAsync(DateOnly today, CancellationToken ct)
    {
        try
        {
            var timesheet = await _attendance.GetSummaryAsync(
                new TimesheetQuery(new DateOnly(today.Year, today.Month, 1), today, null, null, null, null), TimesheetScope.All, ct);
            return timesheet.IsSuccess ? timesheet.Data!.MissingPunchCount : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Số mã hàng có công đoạn chưa có đơn giá (chưa gửi / chưa duyệt) và số mã hàng có đơn giá bị trả lại.</summary>
    private async Task<(int Missing, int Rejected)> PieceRateGapsAsync(CancellationToken ct)
    {
        var rates = _uow.Repository<PieceRate>().Query().AsNoTracking();
        var missing = await _uow.Repository<Operation>().Query().AsNoTracking()
            .Where(o => o.Status == OperationStatuses.Active
                        && !rates.Any(r => r.OperationId == o.OperationId
                                           && (r.Status == PieceRateStatuses.Approved || r.Status == PieceRateStatuses.PendingApproval)))
            .Select(o => o.ProductId).Distinct().CountAsync(ct);
        var rejected = await rates.Where(r => r.Status == PieceRateStatuses.Rejected)
            .Select(r => r.Operation.ProductId).Distinct().CountAsync(ct);
        return (missing, rejected);
    }

    /// <summary>Đơn hàng chưa chốt giá trị (bỏ đơn đã hủy / dừng): tổng số và 5 đơn mới nhất.</summary>
    private async Task<(int Count, IReadOnlyList<HomeItemDto> Items)> OrdersNotSetAsync(CancellationToken ct)
    {
        var query = _uow.Repository<CustomerOrder>().Query().AsNoTracking()
            .Where(o => o.FinalAmount == null && o.Status != OrderStatuses.Cancelled && o.Status != OrderStatuses.Stopped);
        var rows = await query.OrderByDescending(o => o.OrderId).Take(LatestCount)
            .Select(o => new { o.OrderCode, o.Customer.CustomerName }).ToListAsync(ct);
        var items = rows.Select(o => new HomeItemDto(o.OrderCode, o.CustomerName, "Chưa chốt", "orange")).ToList();
        return (await query.CountAsync(ct), items);
    }

    /// <summary>Đơn mua chưa / mới thanh toán một phần (bỏ đơn bị từ chối): tổng số và 5 đơn mới nhất.</summary>
    private async Task<(int Count, IReadOnlyList<HomeItemDto> Items)> PurchasesUnpaidAsync(CancellationToken ct)
    {
        var query = _uow.Repository<PurchaseOrder>().Query().AsNoTracking()
            .Where(p => p.GoodsStatus != PurchaseGoodsStatuses.Rejected
                        && (p.PaymentStatus == PurchaseOrderStatuses.Unpaid || p.PaymentStatus == PurchasePaymentStatuses.PartiallyPaid));
        var rows = await query.OrderByDescending(p => p.PurchaseOrderId).Take(LatestCount)
            .Select(p => new { p.PoCode, p.PaymentStatus, Supplier = p.Supplier == null ? null : p.Supplier.SupplierName }).ToListAsync(ct);
        var items = rows.Select(p => p.PaymentStatus == PurchasePaymentStatuses.PartiallyPaid
            ? new HomeItemDto(p.PoCode, p.Supplier, "Một phần", "blue")
            : new HomeItemDto(p.PoCode, p.Supplier, "Chưa TT", "red")).ToList();
        return (await query.CountAsync(ct), items);
    }

    /// <summary>Chi phí tháng đã nhập chưa; trạng thái bảng lương của tháng (null = chưa có).</summary>
    private async Task<(bool CostEntered, string? Payroll)> MonthStatusAsync(string month, CancellationToken ct)
    {
        var period = await _uow.Repository<AccountingPeriod>().Query().AsNoTracking()
            .Where(p => p.PeriodMonth == month).Select(p => (int?)p.PeriodId).FirstOrDefaultAsync(ct);
        if (period is null) return (false, null);
        var cost = await _uow.Repository<MonthlyCost>().Query().AsNoTracking().AnyAsync(c => c.PeriodId == period, ct);
        var payroll = await _uow.Repository<Payroll>().Query().AsNoTracking()
            .Where(p => p.PeriodId == period).Select(p => p.Status).FirstOrDefaultAsync(ct);
        return (cost, payroll is null ? null : CodeText.ToPascal(payroll));
    }
}
