using Microsoft.EntityFrameworkCore;
using NX_MTD.Application.Interfaces;
using NX_MTD.Domain.Constants;
using NX_MTD.Domain.Entities;
using NX_MTD.Infrastructure.Data;

namespace NX_MTD.Infrastructure.Repositories;

/// <summary>Suất ăn: không có thủ tục DB riêng (khác chấm công) nên đọc/ghi thẳng bằng EF.</summary>
public sealed class MealRepository : IMealRepository
{
    private readonly NxMtdDbContext _context;

    public MealRepository(NxMtdDbContext context) => _context = context;

    public Task<DailyMeal?> FindByDateAsync(DateOnly date, CancellationToken ct) =>
        _context.DailyMeals.FirstOrDefaultAsync(m => m.MealDate == date, ct);

    public async Task<DailyMeal> GetOrCreateAsync(DateOnly date, CancellationToken ct)
    {
        var existing = await FindByDateAsync(date, ct);
        if (existing is not null) return existing;

        var meal = new DailyMeal { MealDate = date, Status = MealStatus.Open };
        _context.DailyMeals.Add(meal);

        try
        {
            await _context.SaveChangesAsync(ct);
            return meal;
        }
        catch (DbUpdateException)
        {
            // Request khác đã tạo trước (UQ_DailyMeal_Date); bỏ theo dõi dòng vừa thêm rồi đọc lại.
            _context.Entry(meal).State = EntityState.Detached;
            return await _context.DailyMeals.FirstAsync(m => m.MealDate == date, ct);
        }
    }

    public Task<Team?> GetTeamAsync(int teamId, CancellationToken ct) =>
        _context.Teams.FirstOrDefaultAsync(t => t.TeamId == teamId, ct);

    public async Task<IReadOnlyList<Team>> GetConfirmableTeamsAsync(CancellationToken ct) =>
        await _context.Teams.AsNoTracking()
            .Where(t => t.Status == RecordStatus.Active
                        && (t.TeamType == TeamTypes.Cutting || t.TeamType == TeamTypes.Sewing))
            .OrderBy(t => t.TeamCode)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> GetTeamLeaderIdsAsync(int teamId, CancellationToken ct) =>
        await _context.Employees.AsNoTracking()
            .Where(e => e.TeamId == teamId && e.Status == RecordStatus.Active
                        && (e.Role.RoleCode == RoleCodes.TeamLeader || e.Role.RoleCode == RoleCodes.TeamDeputy))
            .Select(e => e.EmployeeId)
            .ToListAsync(ct);

    public Task<TeamMealConfirmation?> FindConfirmationAsync(int dailyMealId, int teamId, CancellationToken ct) =>
        _context.TeamMealConfirmations
            .FirstOrDefaultAsync(c => c.DailyMealId == dailyMealId && c.TeamId == teamId, ct);

    public async Task<IReadOnlyList<TeamMealConfirmation>> GetConfirmationsAsync(int dailyMealId, CancellationToken ct) =>
        await _context.TeamMealConfirmations
            .Where(c => c.DailyMealId == dailyMealId)
            .ToListAsync(ct);

    public void AddConfirmation(TeamMealConfirmation confirmation) =>
        _context.TeamMealConfirmations.Add(confirmation);

    public Task<int> GetTeamAttendanceCountAsync(int teamId, DateOnly date, CancellationToken ct) =>
        _context.DailyTimesheets.AsNoTracking()
            .Where(t => t.WorkDate == date && t.CheckIn != null && t.Employee.TeamId == teamId)
            .Select(t => t.EmployeeId)
            .Distinct()
            .CountAsync(ct);

    public Task<int> GetTeamMemberCountAsync(int teamId, CancellationToken ct) =>
        _context.Employees.AsNoTracking()
            .CountAsync(e => e.TeamId == teamId && e.Status == RecordStatus.Active, ct);

    public Task<int> GetOfficeAttendanceCountAsync(DateOnly date, CancellationToken ct) =>
        _context.DailyTimesheets.AsNoTracking()
            .Where(t => t.WorkDate == date && t.CheckIn != null
                        && t.Employee.Team != null && t.Employee.Team.TeamType == TeamTypes.Office)
            .Select(t => t.EmployeeId)
            .Distinct()
            .CountAsync(ct);

    public Task<decimal?> GetPreviousMealPriceAsync(DateOnly beforeDate, CancellationToken ct) =>
        _context.DailyMeals.AsNoTracking()
            .Where(m => m.MealDate < beforeDate && m.MealPrice != null)
            .OrderByDescending(m => m.MealDate)
            .Select(m => m.MealPrice)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<(int EmployeeId, int? TeamId)>> GetUnregisteredAttendeesAsync(
        int dailyMealId, DateOnly date, CancellationToken ct)
    {
        var registered = _context.MealRegistrations.AsNoTracking()
            .Where(r => r.DailyMealId == dailyMealId)
            .Select(r => r.EmployeeId);

        return await _context.DailyTimesheets.AsNoTracking()
            .Where(t => t.WorkDate == date && t.CheckIn != null && !registered.Contains(t.EmployeeId))
            .Select(t => new ValueTuple<int, int?>(t.EmployeeId, t.Employee.TeamId))
            .Distinct()
            .ToListAsync(ct);
    }

    public Task<bool> HasCheckInAsync(int employeeId, DateOnly date, CancellationToken ct) =>
        _context.DailyTimesheets.AsNoTracking()
            .AnyAsync(t => t.EmployeeId == employeeId && t.WorkDate == date && t.CheckIn != null, ct);

    public void AddRegistration(MealRegistration registration) =>
        _context.MealRegistrations.Add(registration);

    public Task<MealRegistration?> FindRegistrationAsync(int dailyMealId, int employeeId, CancellationToken ct) =>
        _context.MealRegistrations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.DailyMealId == dailyMealId && r.EmployeeId == employeeId, ct);

    public Task<MealRegistration?> FindRegistrationTrackedAsync(int dailyMealId, int employeeId, CancellationToken ct) =>
        _context.MealRegistrations
            .FirstOrDefaultAsync(r => r.DailyMealId == dailyMealId && r.EmployeeId == employeeId, ct);

    public async Task<IReadOnlyList<MealRegistration>> GetTeamRegistrationsAsync(
        int dailyMealId, int teamId, CancellationToken ct) =>
        await _context.MealRegistrations
            .Include(r => r.Employee)
            .Where(r => r.DailyMealId == dailyMealId && r.TeamId == teamId)
            .OrderBy(r => r.Employee.FullName)
            .ToListAsync(ct);

    public Task<int> CountWillEatAsync(int dailyMealId, int? teamId, CancellationToken ct) =>
        _context.MealRegistrations.AsNoTracking()
            .CountAsync(r => r.DailyMealId == dailyMealId && r.WillEat
                             && (teamId == null || r.TeamId == teamId), ct);

    public Task<int> CountOfficeWillEatAsync(int dailyMealId, CancellationToken ct) =>
        _context.MealRegistrations.AsNoTracking()
            .CountAsync(r => r.DailyMealId == dailyMealId && r.WillEat
                             && r.Team != null && r.Team.TeamType == TeamTypes.Office, ct);

    public Task<Employee?> GetEmployeeAsync(int employeeId, CancellationToken ct) =>
        _context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId, ct);

    public Task SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);
}
