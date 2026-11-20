using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.DTOs;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Services;

/// <summary>
/// DEV-ALL-148: tổng hợp số liệu xét bậc của các công nhân trong kỳ [from, to] từ bảng công (<c>DailyTimesheet</c>),
/// sản lượng công đoạn (<c>OutputRecord</c>) và phiếu lỗi (<c>DefectRecord</c>). Chỉ lấy cột cần dùng, gộp trong bộ nhớ
/// (một tổ ~30 người × 90 ngày nên số dòng nhỏ).
/// </summary>
internal sealed class GradeMetricsReader
{
    private const decimal SecondsPerHour = 3600m;
    private const int Percent = 100;

    private readonly IUnitOfWork _uow;

    public GradeMetricsReader(IUnitOfWork uow) => _uow = uow;

    public sealed record WorkerRef(int WorkerId, DateOnly StartDate);

    public async Task<Dictionary<int, GradeWorkerMetricsDto>> ReadAsync(IReadOnlyCollection<WorkerRef> workers,
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        var ids = workers.Select(w => w.WorkerId).ToList();
        var sheets = await _uow.Repository<DailyTimesheet>().Query().AsNoTracking()
            .Where(t => ids.Contains(t.EmployeeId) && t.WorkDate >= from && t.WorkDate <= to)
            .Select(t => new { t.EmployeeId, t.WorkdayValue, t.WorkedHours, t.OvertimeHours })
            .ToListAsync(ct);
        var outputs = await _uow.Repository<OutputRecord>().Query().AsNoTracking()
            .Where(o => ids.Contains(o.WorkerId) && o.WorkDate >= from && o.WorkDate <= to)
            .Select(o => new OutputRow(o.WorkerId, o.OperationId, o.Quantity, o.Operation.SkillLevel, o.Operation.StandardTimeSec))
            .ToListAsync(ct);
        var defects = await _uow.Repository<DefectRecord>().Query().AsNoTracking()
            .Where(d => ids.Contains(d.ResponsibleWorkerId) && d.Status != DefectStatuses.Cancelled && d.WorkDate >= from && d.WorkDate <= to)
            .Select(d => new { d.ResponsibleWorkerId, d.DefectType, d.Quantity })
            .ToListAsync(ct);

        return workers.ToDictionary(w => w.WorkerId, w =>
        {
            var s = sheets.Where(t => t.EmployeeId == w.WorkerId).ToList();
            var d = defects.Where(x => x.ResponsibleWorkerId == w.WorkerId).ToList();
            int QtyOf(string type) => d.Where(x => x.DefectType == type).Sum(x => x.Quantity);
            return Build(new TimeTotals(s.Count(t => t.WorkdayValue > 0), s.Sum(t => t.WorkdayValue), s.Sum(t => t.WorkedHours), s.Sum(t => t.OvertimeHours)),
                outputs.Where(o => o.WorkerId == w.WorkerId).ToList(),
                QtyOf(DefectTypes.Rework), QtyOf(DefectTypes.Scrap), QtyOf(DefectTypes.OutputError), Months(w.StartDate, to));
        });
    }

    private sealed record OutputRow(int WorkerId, int OperationId, int Quantity, byte? SkillLevel, decimal? StandardTimeSec);

    private sealed record TimeTotals(int Days, decimal Workdays, decimal Hours, decimal Overtime);

    private static GradeWorkerMetricsDto Build(TimeTotals time, List<OutputRow> rows, int rework, int scrap, int outputError, int tenure)
    {
        var output = Math.Max(0, rows.Sum(r => r.Quantity) - outputError);
        var rated = rows.Where(r => r.SkillLevel is not null).ToList();
        var ratedQty = rated.Sum(r => r.Quantity);
        decimal? avgSkill = ratedQty > 0 ? Round(rated.Sum(r => (decimal)r.Quantity * r.SkillLevel!.Value) / ratedQty) : null;
        var standardHours = rows.Sum(r => r.Quantity * (r.StandardTimeSec ?? 0)) / SecondsPerHour;
        decimal? efficiency = standardHours > 0 && time.Hours > 0 ? Round(standardHours / time.Hours * Percent) : null;
        decimal? defectRate = output > 0 ? Round((decimal)(rework + scrap) / output * Percent) : null;
        return new GradeWorkerMetricsDto(tenure, time.Days, Round(time.Workdays), Round(time.Hours), Round(time.Overtime),
            output, rows.Select(r => r.OperationId).Distinct().Count(), avgSkill, efficiency, rework, scrap, defectRate);
    }

    private static int Months(DateOnly start, DateOnly to) =>
        Math.Max(0, (to.Year - start.Year) * 12 + to.Month - start.Month - (to.Day < start.Day ? 1 : 0));

    private static decimal Round(decimal value) => Math.Round(value, 2);
}
