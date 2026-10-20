namespace NX_MTD.Domain.Constants;

/// <summary>Khai báo một nơi; đồng bộ sang FE src/constants/errorCodes.ts.</summary>
public static class ErrorCodes
{
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthAccountLocked = "AUTH_ACCOUNT_LOCKED";
    public const string AuthAccountDisabled = "AUTH_ACCOUNT_DISABLED";
    public const string AuthInvalidRefreshToken = "AUTH_INVALID_REFRESH_TOKEN";
    public const string AuthUnauthorized = "AUTH_UNAUTHORIZED";
    public const string AuthForbidden = "AUTH_FORBIDDEN";
    public const string CommonValidation = "COMMON_VALIDATION";
    public const string CommonNotFound = "COMMON_NOT_FOUND";
    public const string CommonConcurrencyConflict = "COMMON_CONCURRENCY_CONFLICT";
    public const string CommonInternalError = "COMMON_INTERNAL_ERROR";

    // --- Chấm công & suất ăn (Minh) ---
    public const string AttendanceEmployeeNotFound = "ATTENDANCE_EMPLOYEE_NOT_FOUND";
    public const string AttendanceCodeAlreadyMapped = "ATTENDANCE_CODE_ALREADY_MAPPED";
    public const string AttendanceEmployeeHasCode = "ATTENDANCE_EMPLOYEE_HAS_CODE";
    public const string AttendanceCodeNotFound = "ATTENDANCE_CODE_NOT_FOUND";
    public const string AttendancePeriodLocked = "ATTENDANCE_PERIOD_LOCKED";
    public const string AttendanceEmptyBatch = "ATTENDANCE_EMPTY_BATCH";
    public const string AttendanceAdjustmentExists = "ATTENDANCE_ADJUSTMENT_EXISTS";
    public const string AttendanceEmployeeInactive = "ATTENDANCE_EMPLOYEE_INACTIVE";
    public const string AttendanceAdjustmentNoTime = "ATTENDANCE_ADJUSTMENT_NO_TIME";
    public const string AttendanceRequestInvalid = "ATTENDANCE_REQUEST_INVALID";
    public const string AttendanceExcelInvalid = "ATTENDANCE_EXCEL_INVALID";
    public const string AttendanceExcelEmpty = "ATTENDANCE_EXCEL_EMPTY";

    // Kho, cắt (Thái)
    public const string WarehouseInsufficientStock = "WAREHOUSE_INSUFFICIENT_STOCK";
    public const string WarehouseInvalidStatus = "WAREHOUSE_INVALID_STATUS";
    public const string WarehouseInvalidReference = "WAREHOUSE_INVALID_REFERENCE";
    public const string WarehouseOrderClosed = "WAREHOUSE_ORDER_CLOSED";
    public const string WarehouseTeamMismatch = "WAREHOUSE_TEAM_MISMATCH";
    public const string WarehouseInvalidRequest = "WAREHOUSE_INVALID_REQUEST";
    public const string WarehouseMaterialDuplicate = "WAREHOUSE_MATERIAL_DUPLICATE";
    public const string WarehouseMaterialInactive = "WAREHOUSE_MATERIAL_INACTIVE";
    public const string WarehouseOrderMaterialDuplicate = "WAREHOUSE_ORDER_MATERIAL_DUPLICATE";
    public const string WarehouseOrderMaterialInUse = "WAREHOUSE_ORDER_MATERIAL_IN_USE";
    public const string CuttingFabricShortage = "CUTTING_FABRIC_SHORTAGE";
    public const string CuttingPlanExceedsOrder = "CUTTING_PLAN_EXCEEDS_ORDER";
    public const string CuttingQtyExceedsPlan = "CUTTING_QTY_EXCEEDS_PLAN";
    public const string CuttingHandoverExceedsCut = "CUTTING_HANDOVER_EXCEEDS_CUT";
}
