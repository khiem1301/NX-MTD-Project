namespace NX_MTD.Application.DTOs;

// ---------- Tổ trưởng xác nhận suất ăn tổ (UC30, FR 5.1) ----------

/// <summary>Gợi ý số suất ăn hôm nay cho tổ của tổ trưởng, lấy theo số người đã quẹt / còn tick.</summary>
public sealed record TeamMealSuggestionDto(
    DateOnly MealDate, int TeamId, string TeamName, int SuggestedCount,
    int? MealCount, string? Source, DateTime? ConfirmedAt, bool IsLocked);

/// <summary>Tổ trưởng/tổ phó xác nhận đã rà soát checklist; MealCount lấy từ số WillEat.</summary>
public sealed record ConfirmTeamMealDto(DateOnly MealDate, int? MealCount = null);

/// <summary>Một dòng checklist suất ăn của công nhân trong tổ.</summary>
public sealed record TeamMealMemberDto(
    int EmployeeId, string EmployeeCode, string FullName, bool WillEat, bool HasAttendance);

/// <summary>Checklist suất ăn của tổ trong ngày.</summary>
public sealed record TeamMealMembersDto(
    DateOnly MealDate, int TeamId, string TeamName,
    int AttendanceCount, int MealCount, bool IsLocked,
    IReadOnlyList<TeamMealMemberDto> Members);

/// <summary>Tổ trưởng bỏ/bật tick suất ăn hộ công nhân trong tổ.</summary>
public sealed record SetTeamMemberMealDto(DateOnly MealDate, int EmployeeId, bool WillEat);

// ---------- Suất ăn của tôi ----------

/// <summary>Suất ăn hôm nay của chính người đang đăng nhập.</summary>
public sealed record MyMealDto(
    DateOnly MealDate, bool HasAttendance, bool WillEat, bool IsLocked);

/// <summary>Công nhân bỏ/bật lại tick suất ăn của mình (chỉ khi đã quẹt).</summary>
public sealed record SetMyMealDto(DateOnly? MealDate, bool WillEat);

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
