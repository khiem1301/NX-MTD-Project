namespace NX_MTD.Application.DTOs;

// ---------- Tổ trưởng xác nhận suất ăn tổ (UC30, FR 5.1) ----------

/// <summary>Gợi ý số suất ăn hôm nay cho tổ của tổ trưởng, lấy theo số người đã quẹt vân tay.</summary>
public sealed record TeamMealSuggestionDto(
    DateOnly MealDate, int TeamId, string TeamName, int SuggestedCount,
    int? MealCount, string? Source, DateTime? ConfirmedAt, bool IsLocked);

/// <summary>Tổ trưởng/tổ phó xác nhận số suất ăn của tổ mình cho một ngày.</summary>
public sealed record ConfirmTeamMealDto(DateOnly MealDate, int MealCount);

// ---------- Kế toán: suất khách + tổng hợp + đơn giá (UC30, FR 5.2) ----------

/// <summary>Kế toán nhập suất khách cho một ngày.</summary>
public sealed record SetGuestMealDto(DateOnly MealDate, int GuestCount, string? GuestNote);

/// <summary>Một dòng tổ trong bảng tổng hợp; Source null = tổ chưa xác nhận (số là gợi ý từ chấm công).</summary>
public sealed record TeamMealRowDto(
    int TeamId, string TeamName, int MealCount, int AttendanceCount, string? Source, DateTime? ConfirmedAt);

/// <summary>Tổng hợp suất ăn một ngày cho Kế toán (màn "Tổng hợp suất ăn").</summary>
public sealed record DailyMealSummaryDto(
    int DailyMealId, DateOnly MealDate, string Status, int OfficeCount, int GuestCount, string? GuestNote,
    int TeamTotal, int TotalMeals, decimal? MealPrice, decimal? SuggestedMealPrice, decimal? TotalAmount,
    bool AttendanceIncomplete, DateTime? AutoClosedAt, DateTime? ClosedAt, DateTime? KitchenNotifiedAt,
    IReadOnlyList<TeamMealRowDto> Teams);

/// <summary>Kế toán nhập đơn giá suất ăn cho một ngày đã chốt (màn "Đơn giá suất ăn").</summary>
public sealed record SetMealPriceDto(DateOnly MealDate, decimal MealPrice);
