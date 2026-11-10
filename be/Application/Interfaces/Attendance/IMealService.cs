using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC30: suất ăn hằng ngày — tick từ chấm công, bỏ tick cá nhân/tổ trưởng, chốt 08:30.</summary>
public interface IMealService
{
    // ---------- Tổ trưởng ----------

    Task<Result<TeamMealSuggestionDto>> GetTeamSuggestionAsync(int teamId, DateOnly date, CancellationToken ct);

    Task<Result> ConfirmTeamMealAsync(int teamId, ConfirmTeamMealDto dto, CancellationToken ct);

    Task<Result<TeamMealMembersDto>> GetTeamMembersAsync(int teamId, DateOnly date, CancellationToken ct);

    Task<Result> SetTeamMemberMealAsync(int teamId, int actorEmployeeId, SetTeamMemberMealDto dto, CancellationToken ct);

    // ---------- Cá nhân ----------

    Task<Result<MyMealDto>> GetMyMealAsync(int employeeId, DateOnly date, CancellationToken ct);

    Task<Result> SetMyMealAsync(int employeeId, SetMyMealDto dto, CancellationToken ct);

    // ---------- Kế toán ----------

    Task<Result> SetGuestMealAsync(SetGuestMealDto dto, CancellationToken ct);

    Task<Result<DailyMealSummaryDto>> GetSummaryAsync(DateOnly date, CancellationToken ct);

    /// <summary>Đánh dấu đã báo bếp ngoài hệ thống (không gọi bếp thật qua đây).</summary>
    Task<Result> MarkKitchenNotifiedAsync(DateOnly date, CancellationToken ct);

    Task<Result> SetMealPriceAsync(SetMealPriceDto dto, CancellationToken ct);

    // ---------- Job nền (MI-04) ----------

    /// <summary>JOB-02 08:15: seed từ chấm công + nhắc tổ chưa xác nhận.</summary>
    Task RunReminderJobAsync(DateOnly date, CancellationToken ct);

    /// <summary>JOB-03 08:30: seed lần cuối, cộng MealCount/OfficeCount từ WillEat, rồi khóa ngày.</summary>
    Task RunAutoCloseJobAsync(DateOnly date, CancellationToken ct);
}
