using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

/// <summary>Bảng lương + kỳ kế toán: không có thủ tục DB riêng nên đọc/ghi thẳng bằng EF (giống MealRepository).</summary>
public sealed class PayrollRepository : IPayrollRepository
{
    private readonly NxMtdDbContext _context;

    public PayrollRepository(NxMtdDbContext context) => _context = context;

    // ---------- Kỳ kế toán ----------

    public Task<AccountingPeriod?> FindPeriodByMonthAsync(string periodMonth, CancellationToken ct) =>
        _context.AccountingPeriods.FirstOrDefaultAsync(p => p.PeriodMonth == periodMonth, ct);

    public async Task<AccountingPeriod> GetOrCreatePeriodAsync(string periodMonth, CancellationToken ct)
    {
        var existing = await FindPeriodByMonthAsync(periodMonth, ct);
        if (existing is not null) return existing;

        var year = int.Parse(periodMonth[..4]);
        var month = int.Parse(periodMonth[5..7]);
        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var period = new AccountingPeriod
        {
            PeriodMonth = periodMonth, StartDate = start, EndDate = end, Status = RecordStatus.Open,
        };
        _context.AccountingPeriods.Add(period);

        try
        {
            await _context.SaveChangesAsync(ct);
            return period;
        }
        catch (DbUpdateException)
        {
            // Request khác đã tạo trước (UQ_AccountingPeriod); bỏ theo dõi dòng vừa thêm rồi đọc lại.
            _context.Entry(period).State = EntityState.Detached;
            return await _context.AccountingPeriods.FirstAsync(p => p.PeriodMonth == periodMonth, ct);
        }
    }

    // ---------- Bảng lương ----------

    public Task<Payroll?> FindPayrollByPeriodIdAsync(int periodId, bool includePayslips, CancellationToken ct) =>
        Query(includePayslips).FirstOrDefaultAsync(p => p.PeriodId == periodId, ct);

    public Task<Payroll?> FindPayrollByIdAsync(int payrollId, bool includePayslips, CancellationToken ct) =>
        Query(includePayslips).FirstOrDefaultAsync(p => p.PayrollId == payrollId, ct);

    private IQueryable<Payroll> Query(bool includePayslips)
    {
        IQueryable<Payroll> query = _context.Payrolls.Include(p => p.Period);
        if (includePayslips)
            query = query
                .Include(p => p.Payslips).ThenInclude(s => s.Employee).ThenInclude(e => e.Team)
                .Include(p => p.Payslips).ThenInclude(s => s.PayslipItems);
        return query;
    }

    public async Task<(IReadOnlyList<Payroll> Items, int Total)> GetPayrollsAsync(
        string? status, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Payrolls.Include(p => p.Period).Include(p => p.Payslips).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.Status == status);
        query = query.OrderByDescending(p => p.Period.PeriodMonth);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public void AddPayroll(Payroll payroll) => _context.Payrolls.Add(payroll);

    public async Task RemovePayslipsAsync(int payrollId, CancellationToken ct)
    {
        var payslips = await _context.Payslips
            .Include(s => s.PayslipItems)
            .Where(s => s.PayrollId == payrollId)
            .ToListAsync(ct);

        foreach (var payslip in payslips)
            _context.PayslipItems.RemoveRange(payslip.PayslipItems);
        _context.Payslips.RemoveRange(payslips);
    }

    public void AddPayslip(Payslip payslip) => _context.Payslips.Add(payslip);

    public async Task<IReadOnlyDictionary<int, PayrollImportFile>> GetImportFilesAsync(
        IReadOnlyCollection<int> payrollIds, CancellationToken ct)
    {
        var ids = payrollIds.ToArray();
        var rows = await _context.Attachments.AsNoTracking()
            .Where(a => a.OwnerType == AttachmentOwnerTypes.Payroll && a.Purpose == AttachmentPurposes.ImportFile
                && a.IsCurrent && ids.Contains(a.OwnerId))
            .Select(a => new { a.OwnerId, a.FileUrl, a.UploadedAt })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.OwnerId).ToDictionary(g => g.Key,
            g => g.OrderByDescending(r => r.UploadedAt).Select(r => new PayrollImportFile(r.FileUrl, r.UploadedAt)).First());
    }

    public async Task<IReadOnlyList<string>> RemoveImportFilesAsync(int payrollId, CancellationToken ct)
    {
        var old = await _context.Attachments
            .Where(a => a.OwnerType == AttachmentOwnerTypes.Payroll && a.OwnerId == payrollId
                && a.Purpose == AttachmentPurposes.ImportFile)
            .ToListAsync(ct);
        _context.Attachments.RemoveRange(old);
        return old.Select(a => a.FileUrl).ToList();
    }

    public void AddImportFile(int payrollId, string fileUrl, DateTime uploadedAt) =>
        _context.Attachments.Add(new Attachment
        {
            OwnerType = AttachmentOwnerTypes.Payroll, OwnerId = payrollId, Purpose = AttachmentPurposes.ImportFile,
            FileUrl = fileUrl, FileName = Path.GetFileName(fileUrl), IsCurrent = true, UploadedAt = uploadedAt,
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        });

    public async Task<IReadOnlyDictionary<int, string>> GetReopenReasonsAsync(
        IReadOnlyCollection<int> payrollIds, CancellationToken ct)
    {
        var ids = payrollIds.Select(i => (long)i).ToArray();
        var rows = await _context.AuditLogs.AsNoTracking()
            .Where(l => l.TableName == nameof(Payroll) && l.Action == AuditActions.Unlock && l.Reason != null
                && ids.Contains(l.RecordId))
            .OrderByDescending(l => l.AuditLogId)
            .Select(l => new { l.RecordId, l.Reason })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.RecordId).ToDictionary(g => (int)g.Key, g => g.First().Reason!);
    }

    // ---------- Import Excel: tra cứu nhân viên theo mã chấm công (UC31, DEV-MI-09-4) ----------

    public async Task<IReadOnlyDictionary<string, (int EmployeeId, string FullName, string Status, string? GradeCode)>>
        GetEmployeesByAttendanceCodesAsync(IReadOnlyCollection<string> attendanceCodes, CancellationToken ct)
    {
        var codes = attendanceCodes
            .Select(c => int.TryParse(c, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();
        if (codes.Count == 0)
            return new Dictionary<string, (int, string, string, string?)>();

        var rows = await _context.Employees.AsNoTracking()
            .Where(e => e.AttendanceCode != null && codes.Contains(e.AttendanceCode.Value))
            .Select(e => new { e.AttendanceCode, e.EmployeeId, e.FullName, e.Status, e.CurrentGradeCode })
            .ToListAsync(ct);

        return rows.ToDictionary(
            e => e.AttendanceCode!.Value.ToString(CultureInfo.InvariantCulture),
            e => (e.EmployeeId, e.FullName, e.Status, e.CurrentGradeCode));
    }

    // ---------- Xuất dữ liệu tính lương (UC31) ----------

    public async Task<IReadOnlyList<VwPayrollExportDatum>> GetExportRowsAsync(string periodMonth, CancellationToken ct) =>
        await _context.VwPayrollExportData.AsNoTracking()
            .Where(v => v.PeriodMonth == periodMonth)
            .OrderBy(v => v.EmployeeCode)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<int, int>> GetMissingPunchDaysAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var rows = await _context.DailyTimesheets.AsNoTracking()
            .Where(t => t.WorkDate >= fromDate && t.WorkDate <= toDate && t.IsMissingPunch)
            .GroupBy(t => t.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.EmployeeId, r => r.Count);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetDaysWithoutOutputAsync(
        DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var pieceRateEmployeeIds = _context.Employees.AsNoTracking()
            .Where(e => e.PayType == PayTypes.PieceRate)
            .Select(e => e.EmployeeId);

        var rows = await _context.DailyTimesheets.AsNoTracking()
            .Where(t => t.WorkDate >= fromDate && t.WorkDate <= toDate && t.CheckIn != null
                        && pieceRateEmployeeIds.Contains(t.EmployeeId))
            .Where(t => !_context.OutputRecords.AsNoTracking()
                .Any(o => o.WorkerId == t.EmployeeId && o.WorkDate == t.WorkDate))
            .GroupBy(t => t.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.EmployeeId, r => r.Count);
    }

    // ---------- Công nhân xem phiếu lương của mình (UC06) ----------

    public async Task<IReadOnlyList<(string PeriodMonth, decimal NetPay, DateTime ApprovedAt)>>
        GetMyPayslipSummariesAsync(int employeeId, CancellationToken ct)
    {
        var rows = await _context.Payslips.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId
                        && s.Payroll.Status == PayrollStatus.Approved && s.Payroll.ApprovedAt != null)
            .OrderByDescending(s => s.Payroll.Period.PeriodMonth)
            .Select(s => new { s.Payroll.Period.PeriodMonth, s.NetPay, ApprovedAt = s.Payroll.ApprovedAt!.Value })
            .ToListAsync(ct);

        return rows.Select(r => (r.PeriodMonth, r.NetPay, r.ApprovedAt)).ToList();
    }

    public async Task<(Payslip Payslip, DateTime ApprovedAt)?> FindMyPayslipAsync(
        int employeeId, string periodMonth, CancellationToken ct)
    {
        var payslip = await _context.Payslips
            .Include(s => s.PayslipItems)
            .Include(s => s.Payroll).ThenInclude(p => p.Period)
            .FirstOrDefaultAsync(s => s.EmployeeId == employeeId
                                      && s.Payroll.Period.PeriodMonth == periodMonth
                                      && s.Payroll.Status == PayrollStatus.Approved
                                      && s.Payroll.ApprovedAt != null, ct);

        return payslip is null ? null : (payslip, payslip.Payroll.ApprovedAt!.Value);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
