using NX_MTD.Domain.Entities;

namespace NX_MTD.Application.Interfaces;

/// <summary>Truy cập dữ liệu suất ăn (DailyMeal, TeamMealConfirmation). Không có thủ tục DB riêng
/// cho luồng này nên dùng EF trực tiếp, giống AttendanceRepository cho phần đọc.</summary>
public interface IMealRepository
{
    Task<DailyMeal?> FindByDateAsync(DateOnly date, CancellationToken ct);

    /// <summary>Lấy dòng DailyMeal của ngày, tạo mới (Status = OPEN) nếu chưa có.</summary>
    Task<DailyMeal> GetOrCreateAsync(DateOnly date, CancellationToken ct);

    Task<Team?> GetTeamAsync(int teamId, CancellationToken ct);

    /// <summary>Các tổ cắt/may đang hoạt động — chỉ nhóm này có tổ trưởng xác nhận suất ăn;
    /// văn phòng (OFFICE) không có tổ trưởng nên tính thẳng vào DailyMeal.OfficeCount.</summary>
    Task<IReadOnlyList<Team>> GetConfirmableTeamsAsync(CancellationToken ct);

    /// <summary>EmployeeId của tổ trưởng + tổ phó đang làm của tổ — người nhận thông báo nhắc JOB-02 (DEV-MI-04-1).</summary>
    Task<IReadOnlyList<int>> GetTeamLeaderIdsAsync(int teamId, CancellationToken ct);

    /// <summary>Dòng xác nhận của một tổ (tracked, để sửa trực tiếp rồi SaveChanges).</summary>
    Task<TeamMealConfirmation?> FindConfirmationAsync(int dailyMealId, int teamId, CancellationToken ct);

    Task<IReadOnlyList<TeamMealConfirmation>> GetConfirmationsAsync(int dailyMealId, CancellationToken ct);

    void AddConfirmation(TeamMealConfirmation confirmation);

    /// <summary>Số nhân viên của tổ đã có giờ vào hôm đó (bảng công DailyTimesheet, đã tính sẵn từ chấm công).</summary>
    Task<int> GetTeamAttendanceCountAsync(int teamId, DateOnly date, CancellationToken ct);

    /// <summary>Số nhân viên văn phòng (Team.TeamType = OFFICE) đã có giờ vào hôm đó.</summary>
    Task<int> GetOfficeAttendanceCountAsync(DateOnly date, CancellationToken ct);

    /// <summary>Đơn giá của ngày gần nhất trước ngày truyền vào có đơn giá, để gợi ý cho Kế toán (FR 5.4).</summary>
    Task<decimal?> GetPreviousMealPriceAsync(DateOnly beforeDate, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
