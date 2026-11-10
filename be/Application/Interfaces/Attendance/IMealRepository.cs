using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <summary>Truy cập dữ liệu suất ăn (DailyMeal, TeamMealConfirmation, MealRegistration).</summary>
public interface IMealRepository
{
    Task<DailyMeal?> FindByDateAsync(DateOnly date, CancellationToken ct);

    /// <summary>Lấy dòng DailyMeal của ngày, tạo mới (Status = OPEN) nếu chưa có.</summary>
    Task<DailyMeal> GetOrCreateAsync(DateOnly date, CancellationToken ct);

    Task<Team?> GetTeamAsync(int teamId, CancellationToken ct);

    /// <summary>Các tổ cắt/may đang hoạt động — chỉ nhóm này có tổ trưởng xác nhận suất ăn;
    /// văn phòng (OFFICE) không có tổ trưởng nên tính thẳng vào DailyMeal.OfficeCount.</summary>
    Task<IReadOnlyList<Team>> GetConfirmableTeamsAsync(CancellationToken ct);

    /// <summary>EmployeeId của tổ trưởng + tổ phó đang làm của tổ — người nhận thông báo nhắc JOB-02.</summary>
    Task<IReadOnlyList<int>> GetTeamLeaderIdsAsync(int teamId, CancellationToken ct);

    /// <summary>Dòng xác nhận của một tổ (tracked, để sửa trực tiếp rồi SaveChanges).</summary>
    Task<TeamMealConfirmation?> FindConfirmationAsync(int dailyMealId, int teamId, CancellationToken ct);

    Task<IReadOnlyList<TeamMealConfirmation>> GetConfirmationsAsync(int dailyMealId, CancellationToken ct);

    void AddConfirmation(TeamMealConfirmation confirmation);

    /// <summary>Số nhân viên của tổ đã có giờ vào hôm đó (bảng công DailyTimesheet).</summary>
    Task<int> GetTeamAttendanceCountAsync(int teamId, DateOnly date, CancellationToken ct);

    /// <summary>Số người đang làm trong tổ (Employee.TeamId, Status = ACTIVE).</summary>
    Task<int> GetTeamMemberCountAsync(int teamId, CancellationToken ct);

    /// <summary>Số nhân viên văn phòng (Team.TeamType = OFFICE) đã có giờ vào hôm đó.</summary>
    Task<int> GetOfficeAttendanceCountAsync(DateOnly date, CancellationToken ct);

    /// <summary>Đơn giá của ngày gần nhất trước ngày truyền vào có đơn giá.</summary>
    Task<decimal?> GetPreviousMealPriceAsync(DateOnly beforeDate, CancellationToken ct);

    // ---------- MealRegistration (suất theo người) ----------

    /// <summary>Nhân viên đã có giờ vào trong ngày nhưng chưa có dòng MealRegistration.</summary>
    Task<IReadOnlyList<(int EmployeeId, int? TeamId)>> GetUnregisteredAttendeesAsync(
        int dailyMealId, DateOnly date, CancellationToken ct);

    /// <summary>Đã có giờ vào (DailyTimesheet.CheckIn) trong ngày chưa.</summary>
    Task<bool> HasCheckInAsync(int employeeId, DateOnly date, CancellationToken ct);

    void AddRegistration(MealRegistration registration);

    Task<MealRegistration?> FindRegistrationAsync(int dailyMealId, int employeeId, CancellationToken ct);

    Task<MealRegistration?> FindRegistrationTrackedAsync(int dailyMealId, int employeeId, CancellationToken ct);

    Task<IReadOnlyList<MealRegistration>> GetTeamRegistrationsAsync(int dailyMealId, int teamId, CancellationToken ct);

    Task<int> CountWillEatAsync(int dailyMealId, int? teamId, CancellationToken ct);

    /// <summary>Số suất văn phòng (WillEat) — TeamType = OFFICE.</summary>
    Task<int> CountOfficeWillEatAsync(int dailyMealId, CancellationToken ct);

    Task<Employee?> GetEmployeeAsync(int employeeId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
