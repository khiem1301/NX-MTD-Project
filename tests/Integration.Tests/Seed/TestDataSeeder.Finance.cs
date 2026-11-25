using Microsoft.EntityFrameworkCore;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace Integration.Tests.Seed;

public sealed partial class TestDataSeeder
{
    private async Task SeedFinanceAsync(CancellationToken ct)
    {
        var curMonth = $"{_t.Year:D4}-{_t.Month:D2}";
        var prevDate = new DateOnly(_t.Year, _t.Month, 1).AddDays(-1);
        var prevMonth = $"{prevDate.Year:D4}-{prevDate.Month:D2}";

        _ctx.PeriodCur = await EnsurePeriodAsync(curMonth, RecordStatus.Open, lockedAt: null, ct);
        _ctx.PeriodPrev = await EnsurePeriodAsync(prevMonth, RecordStatus.Locked, lockedAt: _now.AddDays(-1), ct);

        // Hai kỳ phụ cho PENDING_APPROVAL / REJECTED payroll (PeriodId unique trên Payroll)
        var pendingMonth = $"{_t.AddMonths(1).Year:D4}-{_t.AddMonths(1).Month:D2}";
        var rejectedDate = new DateOnly(_t.Year, _t.Month, 1).AddMonths(-2);
        var rejectedMonth = $"{rejectedDate.Year:D4}-{rejectedDate.Month:D2}";
        _ctx.PeriodPendingPayroll = await EnsurePeriodAsync(pendingMonth, RecordStatus.Open, null, ct);
        _ctx.PeriodRejectedPayroll = await EnsurePeriodAsync(rejectedMonth, RecordStatus.Locked, _now.AddMonths(-2), ct);

        _ctx.PayrollDraft = await EnsurePayrollAsync(_ctx.PeriodCur.PeriodId, PayrollStatus.Draft, ct);
        _ctx.PayrollApproved = await EnsurePayrollAsync(
            _ctx.PeriodPrev.PeriodId, PayrollStatus.Approved, ct,
            submittedAt: _now.AddMonths(-1), approvedAt: _now.AddMonths(-1).AddDays(1));
        _ctx.PayrollPending = await EnsurePayrollAsync(
            _ctx.PeriodPendingPayroll.PeriodId, PayrollStatus.PendingApproval, ct,
            submittedAt: _now);
        _ctx.PayrollRejected = await EnsurePayrollAsync(
            _ctx.PeriodRejectedPayroll.PeriodId, PayrollStatus.Rejected, ct,
            submittedAt: _now.AddMonths(-2), rejectReason: "IT reject payroll");

        _ctx.PayslipWorkerSewA1 = new Payslip
        {
            PayrollId = _ctx.PayrollApproved.PayrollId,
            EmployeeId = _ctx.WorkerSewA1.EmployeeId,
            WorkDays = 22m,
            TotalAdditions = 5_000_000m,
            TotalDeductions = 200_000m,
            Insurance = 500_000m,
            NetPay = 4_300_000m,
        };
        _ctx.PayslipWorkerSewA2 = new Payslip
        {
            PayrollId = _ctx.PayrollApproved.PayrollId,
            EmployeeId = _ctx.WorkerSewA2.EmployeeId,
            WorkDays = 20m,
            TotalAdditions = 4_500_000m,
            TotalDeductions = 150_000m,
            Insurance = 450_000m,
            NetPay = 3_900_000m,
        };
        _db.Payslips.AddRange(_ctx.PayslipWorkerSewA1, _ctx.PayslipWorkerSewA2);

        // CUR: đủ chi phí FIXED + VARIABLE (basis OUTPUT / LABOUR_HOURS)
        foreach (var cat in new[]
                 {
                     MonthlyCostCategory.Meals, MonthlyCostCategory.Fuel, MonthlyCostCategory.Utilities,
                     MonthlyCostCategory.Premises, MonthlyCostCategory.Other,
                 })
        {
            _ctx.MonthlyCostsCur.Add(await EnsureMonthlyCostAsync(
                _ctx.PeriodCur.PeriodId, cat, MonthlyCostType.Fixed, productId: null, 1_000_000m, ct));
        }

        _ctx.MonthlyCostsCur.Add(await EnsureMonthlyCostAsync(
            _ctx.PeriodCur.PeriodId, MonthlyCostCategory.MaterialsBought, MonthlyCostType.Variable,
            _ctx.Product001.ProductId, 2_000_000m, ct));

        // PREV: thiếu chi phí — dùng category khác demo nếu MEALS đã có, hoặc reuse.
        var prevExisting = await _db.MonthlyCosts
            .Where(c => c.PeriodId == _ctx.PeriodPrev.PeriodId)
            .ToListAsync(ct);
        if (prevExisting.Count == 0)
        {
            _ctx.MonthlyCostsPrev.Add(await EnsureMonthlyCostAsync(
                _ctx.PeriodPrev.PeriodId, MonthlyCostCategory.Meals, MonthlyCostType.Fixed,
                null, 500_000m, ct));
        }
        else
        {
            // Giữ sparse: chỉ để lại 1 dòng trong tracker ngữ cảnh (không xóa demo ngoài transaction).
            _ctx.MonthlyCostsPrev.AddRange(prevExisting.Take(1));
        }
    }

    private async Task<MonthlyCost> EnsureMonthlyCostAsync(
        int periodId, string category, string costType, int? productId, decimal amount, CancellationToken ct)
    {
        var existing = await _db.MonthlyCosts.FirstOrDefaultAsync(
            c => c.PeriodId == periodId
                 && c.CostCategoryCode == category
                 && c.ProductId == productId, ct);
        if (existing is not null)
        {
            existing.Amount = amount;
            existing.CostType = costType;
            existing.ImportedAt = _now;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var cost = new MonthlyCost
        {
            PeriodId = periodId,
            CostCategoryCode = category,
            CostType = costType,
            ProductId = productId,
            Amount = amount,
            ImportedAt = _now,
        };
        _db.MonthlyCosts.Add(cost);
        await _db.SaveChangesAsync(ct);
        return cost;
    }

    private async Task<AccountingPeriod> EnsurePeriodAsync(
        string periodMonth, string status, DateTime? lockedAt, CancellationToken ct)
    {
        var start = DateOnly.ParseExact(periodMonth + "-01", "yyyy-MM-dd");
        var end = start.AddMonths(1).AddDays(-1);

        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.PeriodMonth == periodMonth, ct);
        if (period is null)
        {
            period = new AccountingPeriod
            {
                PeriodMonth = periodMonth,
                StartDate = start,
                EndDate = end,
                Status = status,
                LockedAt = lockedAt,
            };
            _db.AccountingPeriods.Add(period);
            await _db.SaveChangesAsync(ct);
            return period;
        }

        period.Status = status;
        period.LockedAt = lockedAt;
        await _db.SaveChangesAsync(ct);
        return period;
    }

    private async Task<Payroll> EnsurePayrollAsync(
        int periodId,
        string status,
        CancellationToken ct,
        DateTime? submittedAt = null,
        DateTime? approvedAt = null,
        string? rejectReason = null)
    {
        var payroll = await _db.Payrolls.FirstOrDefaultAsync(p => p.PeriodId == periodId, ct);
        if (payroll is null)
        {
            payroll = new Payroll
            {
                PeriodId = periodId,
                Status = status,
                SubmittedAt = submittedAt,
                ApprovedAt = approvedAt,
                RejectReason = rejectReason,
            };
            _db.Payrolls.Add(payroll);
            await _db.SaveChangesAsync(ct);
            return payroll;
        }

        payroll.Status = status;
        payroll.SubmittedAt = submittedAt;
        payroll.ApprovedAt = approvedAt;
        payroll.RejectReason = rejectReason;
        await _db.SaveChangesAsync(ct);
        return payroll;
    }
}
