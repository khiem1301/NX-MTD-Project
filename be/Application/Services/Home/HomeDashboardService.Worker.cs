using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

public sealed partial class HomeDashboardService
{
    /// <summary>Số ngày gần nhất của phiếu lỗi hiện trên trang chủ công nhân.</summary>
    private const int RecentDefectDays = 7;

    /// <summary>Công nhân: lương tạm tính, phiếu lương mới nhất, việc chờ xác nhận, phiếu lỗi mới, suất ăn hôm nay.</summary>
    public async Task<Result<WorkerHomeDto>> GetWorkerAsync(CancellationToken ct)
    {
        var me = _user.EmployeeId;
        var today = _clock.Today;
        var (estToday, estMonth) = await EstimatedPayAsync(me, today, ct);
        var allocations = await AllocationItemsAsync(me, ct);
        var defects = await RecentDefectsAsync(me, today, ct);
        var meal = await _meals.GetMyMealAsync(me, today, ct);
        return Result<WorkerHomeDto>.Success(new WorkerHomeDto(
            estToday, estMonth, await LatestPayslipAsync(me, ct), allocations.Count, allocations.Items, defects,
            meal.IsSuccess && meal.Data!.HasAttendance, meal.IsSuccess && meal.Data!.WillEat, meal.IsSuccess && meal.Data!.IsLocked));
    }

    /// <summary>Lương tạm tính hôm nay và từ đầu tháng; null khi công nhân không khoán sản phẩm (không có ngày nào).</summary>
    private async Task<(decimal? Today, decimal? Month)> EstimatedPayAsync(int employeeId, DateOnly today, CancellationToken ct)
    {
        var result = await _pay.GetMyEstimatedPayAsync(
            employeeId, new MyEstimatedPayQuery { From = new DateOnly(today.Year, today.Month, 1), To = today }, ct);
        if (!result.IsSuccess || result.Data!.Days.Count == 0) return (null, null);
        var day = result.Data.Days.FirstOrDefault(d => d.WorkDate == today);
        return (day?.EstimatedPay ?? 0, result.Data.TotalEstimatedPay);
    }

    private async Task<HomePayslipDto?> LatestPayslipAsync(int employeeId, CancellationToken ct)
    {
        try
        {
            return await _uow.Repository<Payslip>().Query().AsNoTracking()
                .Where(p => p.EmployeeId == employeeId && p.Payroll.Status == PayrollStatus.Approved)
                .OrderByDescending(p => p.Payroll.Period.PeriodMonth)
                .Select(p => new HomePayslipDto(p.Payroll.Period.PeriodMonth, p.NetPay)).FirstOrDefaultAsync(ct);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Giao việc đang chờ công nhân xác nhận: tổng số và 5 phiếu mới nhất.</summary>
    private async Task<(int Count, IReadOnlyList<HomeItemDto> Items)> AllocationItemsAsync(int workerId, CancellationToken ct)
    {
        var query = _uow.Repository<WorkAllocation>().Query().AsNoTracking()
            .Where(a => a.WorkerId == workerId && a.Status == HandoverStatuses.Pending);
        var rows = await query.OrderByDescending(a => a.WorkAllocationId).Take(LatestCount)
            .Select(a => new { a.Operation.OperationName, a.Quantity, Order = a.OrderLine.Order.OrderCode }).ToListAsync(ct);
        var items = rows.Select(a => new HomeItemDto($"{a.OperationName} · {a.Quantity} sp", a.Order, "Nhận", "blue")).ToList();
        return (await query.CountAsync(ct), items);
    }

    /// <summary>Phiếu lỗi ghi cho công nhân trong 7 ngày gần nhất (bỏ phiếu đã hủy).</summary>
    private async Task<IReadOnlyList<HomeItemDto>> RecentDefectsAsync(int workerId, DateOnly today, CancellationToken ct)
    {
        var since = today.AddDays(-RecentDefectDays);
        var rows = await _uow.Repository<DefectRecord>().Query().AsNoTracking()
            .Where(d => d.ResponsibleWorkerId == workerId && d.Status != DefectStatuses.Cancelled && d.WorkDate >= since)
            .OrderByDescending(d => d.DefectRecordId).Take(LatestCount)
            .Select(d => new { d.DefectType, d.Quantity, d.Operation.OperationName, d.WorkDate }).ToListAsync(ct);
        return rows.Select(d => new HomeItemDto(
            $"{CodeText.ToPascal(d.DefectType)} {d.Quantity} sp · {d.OperationName}", d.WorkDate.ToString("dd/MM"), "Mới", "red")).ToList();
    }
}
