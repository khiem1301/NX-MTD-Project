namespace NX_MTD.Domain.Constants;

/// <summary>Nguồn dữ liệu của một phiên đồng bộ chấm công (AttendanceSyncSession.Source).</summary>
public static class SyncSource
{
    public const string RjSync = "RJ_SYNC";
    public const string ExcelImport = "EXCEL_IMPORT";
}

/// <summary>Trạng thái phiên đồng bộ (AttendanceSyncSession.Status).</summary>
public static class SyncStatus
{
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}

/// <summary>Nguồn của một dòng bảng công ngày (DailyTimesheet.Source).</summary>
public static class TimesheetSource
{
    public const string Fingerprint = "FINGERPRINT";
    public const string Adjustment = "ADJUSTMENT";
}

/// <summary>Loại chấm hộ của Kế toán (AttendanceAdjustment.AdjustmentType).</summary>
public static class AdjustmentType
{
    public const string ForgotPunch = "FORGOT_PUNCH";
    public const string FingerprintFail = "FINGERPRINT_FAIL";
    public const string OffsiteWork = "OFFSITE_WORK";
    public const string ApprovedRequest = "APPROVED_REQUEST";

    public static readonly string[] All = [ForgotPunch, FingerprintFail, OffsiteWork, ApprovedRequest];
}

/// <summary>Khóa tham số trong bảng SystemSetting — khai báo một nơi cho mọi luồng dùng.</summary>
public static class SettingKeys
{
    public const string AttendanceSyncMinutes = "ATTENDANCE_SYNC_MINUTES";
    /// <summary>Dung sai xăng vượt định mức (%) trước khi cảnh báo, mặc định 15 (MSG30, UC35).</summary>
    public const string FuelNormTolerancePercent = "FUEL_NORM_TOLERANCE_PERCENT";
    public const string AttendanceAlertMinutes = "ATTENDANCE_ALERT_MINUTES";
    public const string WorkStartTime = "WORK_START_TIME";
    public const string WorkEndTime = "WORK_END_TIME";
    public const string LunchStartTime = "LUNCH_START_TIME";
    public const string LunchEndTime = "LUNCH_END_TIME";
    public const string WorkdayStandardHours = "WORKDAY_STANDARD_HOURS";
    public const string DuplicatePunchMinutes = "DUPLICATE_PUNCH_MINUTES";
    public const string MealReminderTime = "MEAL_REMINDER_TIME";
    public const string MealCutoffTime = "MEAL_CUTOFF_TIME";

    // Tiến độ đơn, thông báo (Khiêm, KH-16 / KH-23)
    public const string DelayCheckTime = "DELAY_CHECK_TIME";
    public const string DelayAvgDays = "DELAY_AVG_DAYS";
    public const string NotificationRetentionDays = "NOTIFICATION_RETENTION_DAYS";
    /// <summary>Giờ chạy JOB-05; khóa chưa seed trong SystemSetting nên dùng mặc định 01:00 (DEV-KH-23-1).</summary>
    public const string NotificationCleanupTime = "NOTIFICATION_CLEANUP_TIME";

    // Kho, cắt (Thái)
    public const string CutSurplusPercent = "CUT_SURPLUS_PERCENT";

    // Đơn từ (Khiêm)
    public const string RequestMaxPhotos = "REQUEST_MAX_PHOTOS";

    // KPI tổ may (Minh, UC19 KPI tổ may — DEV-MI-16-2: khóa chưa seed trong SystemSetting,
    // dùng fallback mặc định của KpiAutoCloseJob)
    public const string KpiAutoCloseTime = "KPI_AUTO_CLOSE_TIME";
}

/// <summary>Trạng thái suất ăn ngày (DailyMeal.Status) — UC30.</summary>
public static class MealStatus
{
    public const string Open = "OPEN";
    public const string AutoClosed = "AUTO_CLOSED";
    public const string Closed = "CLOSED";
    public const string KitchenNotified = "KITCHEN_NOTIFIED";
}

/// <summary>Nguồn số suất ăn của tổ (TeamMealConfirmation.Source) — UC30.</summary>
public static class MealConfirmationSource
{
    /// <summary>Tổ trưởng/tổ phó tự xác nhận.</summary>
    public const string Confirmed = "CONFIRMED";

    /// <summary>Hệ thống lấy theo số người đã quẹt vân tay (JOB-02 nhắc hoặc JOB-03 chốt).</summary>
    public const string Attendance = "ATTENDANCE";
}
