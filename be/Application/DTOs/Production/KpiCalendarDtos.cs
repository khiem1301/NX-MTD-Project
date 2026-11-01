namespace NX_MTD.Application.DTOs;

/// <summary>Một ngày đặt KPI; ngày không gửi lên nghĩa là không đặt KPI ngày đó.</summary>
public sealed record KpiDailyTargetInputDto(DateOnly WorkDate, int TargetQty);

/// <summary>Toàn bộ KPI theo ngày của một chỉ tiêu (ghi đè bản đã lưu).</summary>
public sealed record SaveKpiDailyPlanDto(IReadOnlyList<KpiDailyTargetInputDto> DailyTargets);

/// <summary>Một ô trên lịch KPI. TargetQty = 0 là chưa đặt; ActualQty = hàng QA/QC nhập cho tổ, null ở ngày chưa tới.</summary>
public sealed record KpiCalendarDayDto(DateOnly WorkDate, int TargetQty, int? ActualQty);

/// <summary>
/// BTP khả dụng cho KPI theo ngày của một tổ: BTP bộ đủ còn trong kho + BTP tổ đang giữ − phần KPI ngày của tổ khác phải lấy
/// từ kho (KPI đã đặt chưa hoàn thiện mà BTP tổ đó đang giữ không đủ).
/// </summary>
public sealed record KpiBtpDto(int WarehouseBtp, int TeamHeld, int NeededByOthers, int Available);

/// <summary>Lịch KPI theo ngày của một chỉ tiêu. FinishedQty = hàng hoàn thiện của tổ trong kỳ. FirstEditableDate = ngày sớm nhất còn sửa được KPI, hôm nay nếu chưa tới EditCutoff.</summary>
public sealed record KpiCalendarDto(
    int KpiTargetId, string OrderCode, string ProductCode, string ProductName, string SewingTeamName,
    DateOnly StartDate, DateOnly EndDate, DateOnly Today, DateOnly DueDate, DateOnly FirstEditableDate, TimeOnly EditCutoff,
    int TargetFinishedQty, int FinishedQty, bool CanEdit, KpiBtpDto Btp, IReadOnlyList<KpiCalendarDayDto> Days);
