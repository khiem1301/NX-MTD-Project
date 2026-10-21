using NX_MTD.Application.Common;
using NX_MTD.Application.DTOs;

namespace NX_MTD.Application.Interfaces;

/// <summary>UC30: suất ăn hằng ngày — tổ trưởng xác nhận số suất tổ, Kế toán nhập suất khách,
/// xem tổng hợp và nhập đơn giá. JOB-02/JOB-03 cũng gọi qua interface này.</summary>
public interface IMealService
{
    // ---------- Tổ trưởng ----------

    Task<Result<TeamMealSuggestionDto>> GetTeamSuggestionAsync(int teamId, DateOnly date, CancellationToken ct);

    Task<Result> ConfirmTeamMealAsync(int teamId, ConfirmTeamMealDto dto, CancellationToken ct);

    // ---------- Kế toán ----------

    Task<Result> SetGuestMealAsync(SetGuestMealDto dto, CancellationToken ct);

    Task<Result<DailyMealSummaryDto>> GetSummaryAsync(DateOnly date, CancellationToken ct);

    /// <summary>Đánh dấu đã báo bếp ngoài hệ thống (không gọi bếp thật qua đây).</summary>
    Task<Result> MarkKitchenNotifiedAsync(DateOnly date, CancellationToken ct);

    Task<Result> SetMealPriceAsync(SetMealPriceDto dto, CancellationToken ct);

    // ---------- Job nền (MI-04) ----------

    /// <summary>JOB-02 08:15: với tổ chưa xác nhận, ghi nhận số gợi ý hiện tại + mốc đã nhắc.</summary>
    Task RunReminderJobAsync(DateOnly date, CancellationToken ct);

    /// <summary>JOB-03 08:30: tổ chưa xác nhận lấy theo số đã quẹt, văn phòng lấy theo số đã quẹt, rồi khóa ngày.</summary>
    Task RunAutoCloseJobAsync(DateOnly date, CancellationToken ct);
}
