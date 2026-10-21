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

    // Chung (Phase H)
    public const string CommonDuplicate = "COMMON_DUPLICATE";
    public const string CommonConstraintViolation = "COMMON_CONSTRAINT_VIOLATION";
    public const string CommonDbRule = "COMMON_DB_RULE";

    // Đính kèm (KH-07)
    public const string AttachmentOwnerTypeInvalid = "ATTACHMENT_OWNER_TYPE_INVALID";
    public const string AttachmentPurposeInvalid = "ATTACHMENT_PURPOSE_INVALID";
    public const string AttachmentFileEmpty = "ATTACHMENT_FILE_EMPTY";
    public const string AttachmentFileTooLarge = "ATTACHMENT_FILE_TOO_LARGE";
    public const string AttachmentFileTypeNotAllowed = "ATTACHMENT_FILE_TYPE_NOT_ALLOWED";
    public const string AttachmentLimitReached = "ATTACHMENT_LIMIT_REACHED";

    // Đơn từ (KH-06)
    public const string RequestTypeNotAllowed = "REQUEST_TYPE_NOT_ALLOWED";
    public const string RequestInvalidStatus = "REQUEST_INVALID_STATUS";
    public const string RequestManualCompleteNotAllowed = "REQUEST_MANUAL_COMPLETE_NOT_ALLOWED";

    // Mã hàng, đơn hàng (KH-08)
    public const string ProductCodeDuplicate = "PRODUCT_CODE_DUPLICATE";
    public const string ProductCodeLocked = "PRODUCT_CODE_LOCKED";
    public const string ProductNotActive = "PRODUCT_NOT_ACTIVE";
    public const string ProductInvalidStatus = "PRODUCT_INVALID_STATUS";
    public const string OrderCodeDuplicate = "ORDER_CODE_DUPLICATE";
    public const string OrderNotEditable = "ORDER_NOT_EDITABLE";
    public const string OrderLineRemoveNotAllowed = "ORDER_LINE_REMOVE_NOT_ALLOWED";
    public const string OrderLineKeyLocked = "ORDER_LINE_KEY_LOCKED";
    public const string OrderInvalidStatus = "ORDER_INVALID_STATUS";
    public const string CustomerNameDuplicate = "CUSTOMER_NAME_DUPLICATE";

    // Nhân sự, tài khoản, tổ (KH-10)
    public const string AuthWrongCurrentPassword = "AUTH_WRONG_CURRENT_PASSWORD";
    public const string EmployeeCodeDuplicate = "EMPLOYEE_CODE_DUPLICATE";
    public const string EmployeeAttendanceCodeTaken = "EMPLOYEE_ATTENDANCE_CODE_TAKEN";
    public const string EmployeeNotActive = "EMPLOYEE_NOT_ACTIVE";
    public const string AccountCannotLockSelf = "ACCOUNT_CANNOT_LOCK_SELF";
    public const string AccountInvalidStatus = "ACCOUNT_INVALID_STATUS";
    public const string TeamCodeDuplicate = "TEAM_CODE_DUPLICATE";
    public const string TeamNameDuplicate = "TEAM_NAME_DUPLICATE";
    public const string TeamNotActive = "TEAM_NOT_ACTIVE";
    public const string TeamLeaderInvalidTeam = "TEAM_LEADER_INVALID_TEAM";
    public const string TeamLeaderExists = "TEAM_LEADER_EXISTS";
    public const string TeamDeputyExists = "TEAM_DEPUTY_EXISTS";
    public const string TeamHasActiveMembers = "TEAM_HAS_ACTIVE_MEMBERS";
    public const string TeamTransferFutureDate = "TEAM_TRANSFER_FUTURE_DATE";
    public const string TeamTransferLeader = "TEAM_TRANSFER_LEADER";
    public const string TeamMemberNotInTeam = "TEAM_MEMBER_NOT_IN_TEAM";

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
