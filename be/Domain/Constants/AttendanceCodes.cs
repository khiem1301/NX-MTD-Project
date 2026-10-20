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

/// <summary>Loại đơn từ (RequestTypeCode) — khai báo một nơi cho mọi luồng dùng.</summary>
public static class RequestTypes
{
    public const string AttendanceCorrection = "ATTENDANCE_CORRECTION";

    // Kho, cắt (Thái)
    public const string MaterialRequisition = "MATERIAL_REQUISITION";
    public const string SemiFinishedRequisition = "SEMI_FINISHED_REQUISITION";
}

/// <summary>Trạng thái đơn từ (đồng bộ với CHECK CK_Request_Status trong DB).</summary>
public static class RequestStatus
{
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string PendingProcessing = "PENDING_PROCESSING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
}

/// <summary>Khóa tham số trong bảng SystemSetting — khai báo một nơi cho mọi luồng dùng.</summary>
public static class SettingKeys
{
    public const string AttendanceSyncMinutes = "ATTENDANCE_SYNC_MINUTES";
    public const string AttendanceAlertMinutes = "ATTENDANCE_ALERT_MINUTES";
    public const string WorkStartTime = "WORK_START_TIME";
    public const string WorkEndTime = "WORK_END_TIME";
    public const string LunchStartTime = "LUNCH_START_TIME";
    public const string LunchEndTime = "LUNCH_END_TIME";
    public const string WorkdayStandardHours = "WORKDAY_STANDARD_HOURS";
    public const string DuplicatePunchMinutes = "DUPLICATE_PUNCH_MINUTES";
    public const string MealReminderTime = "MEAL_REMINDER_TIME";
    public const string MealCutoffTime = "MEAL_CUTOFF_TIME";

    // Kho, cắt (Thái)
    public const string CutSurplusPercent = "CUT_SURPLUS_PERCENT";
}
