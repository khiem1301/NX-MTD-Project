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
    public const string ProductSpecInvalid = "PRODUCT_SPEC_INVALID";
    public const string KpiExceedsStock = "KPI_EXCEEDS_STOCK";
    public const string ReceiptPhotoRequired = "RECEIPT_PHOTO_REQUIRED";
    public const string ReceiptDuplicatePending = "RECEIPT_DUPLICATE_PENDING";
    public const string ProductNotActive = "PRODUCT_NOT_ACTIVE";
    public const string ProductInvalidStatus = "PRODUCT_INVALID_STATUS";
    public const string OrderCodeDuplicate = "ORDER_CODE_DUPLICATE";
    public const string OrderNotEditable = "ORDER_NOT_EDITABLE";
    public const string OrderLineRemoveNotAllowed = "ORDER_LINE_REMOVE_NOT_ALLOWED";
    public const string OrderLineKeyLocked = "ORDER_LINE_KEY_LOCKED";
    public const string OrderInvalidStatus = "ORDER_INVALID_STATUS";
    public const string OrderMaterialsAwaiting = "ORDER_MATERIALS_AWAITING";
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

    // --- Suất ăn (Minh, UC30) ---
    public const string MealTeamNotFound = "MEAL_TEAM_NOT_FOUND";
    public const string MealNotFound = "MEAL_NOT_FOUND";
    public const string MealLocked = "MEAL_LOCKED";               // trigger trg_TeamMeal_Cutoff (THROW 50020)
    public const string MealGuestLocked = "MEAL_GUEST_LOCKED";     // trigger trg_DailyMeal_GuestCutoff (THROW 50021)
    public const string MealNotClosedYet = "MEAL_NOT_CLOSED_YET";

    // --- Bảng lương + khóa tháng (Minh, UC06/UC31/UC32) ---
    public const string PayrollNotFound = "PAYROLL_NOT_FOUND";
    public const string PayrollInvalidStatus = "PAYROLL_INVALID_STATUS";
    public const string PayrollEmpty = "PAYROLL_EMPTY";
    public const string PayrollReasonRequired = "PAYROLL_REASON_REQUIRED";
    public const string PayrollPeriodLocked = "PAYROLL_PERIOD_LOCKED";
    public const string PayrollExcelInvalid = "PAYROLL_EXCEL_INVALID";
    public const string PayrollExcelEmpty = "PAYROLL_EXCEL_EMPTY";
    public const string PayrollFileNotFound = "PAYROLL_FILE_NOT_FOUND";
    public const string PayrollPeriodMonthInvalid = "PAYROLL_PERIOD_MONTH_INVALID";
    public const string PayslipNotFound = "PAYSLIP_NOT_FOUND";

    // --- Chi phí tháng (Minh, UC12/UC33) ---
    public const string MonthlyCostNotFound = "MONTHLY_COST_NOT_FOUND";
    public const string MonthlyCostPeriodMonthInvalid = "MONTHLY_COST_PERIOD_MONTH_INVALID";
    public const string MonthlyCostPeriodLocked = "MONTHLY_COST_PERIOD_LOCKED";
    public const string MonthlyCostExcelInvalid = "MONTHLY_COST_EXCEL_INVALID";
    public const string MonthlyCostExcelEmpty = "MONTHLY_COST_EXCEL_EMPTY";

    // --- KPI tổ may (Minh, UC19 KPI tổ may — khác UC19 lệnh cắt ở WorkOrderService) ---
    public const string KpiNotFound = "KPI_NOT_FOUND";
    public const string KpiInvalidStatus = "KPI_INVALID_STATUS";
    public const string KpiInvalidReference = "KPI_INVALID_REFERENCE";
    public const string KpiDuplicatePeriod = "KPI_DUPLICATE_PERIOD";

    // --- Doanh thu + hòa vốn (Minh, UC27/UC11) ---
    public const string RevenuePeriodMonthInvalid = "REVENUE_PERIOD_MONTH_INVALID";
    public const string BreakEvenPeriodMonthInvalid = "BREAK_EVEN_PERIOD_MONTH_INVALID";
    public const string BreakEvenInvalidAllocationBasis = "BREAK_EVEN_INVALID_ALLOCATION_BASIS";
    public const string BreakEvenNotReady = "BREAK_EVEN_NOT_READY";                 // MSG25 (GBR-17)
    public const string BreakEvenNoOutput = "BREAK_EVEN_NO_OUTPUT";
    public const string BreakEvenNotFound = "BREAK_EVEN_NOT_FOUND";

    // --- Báo cáo kế toán (Minh, UC36 — CHỈ Kế toán) ---
    public const string AccountingReportPeriodMonthInvalid = "ACCOUNTING_REPORT_PERIOD_MONTH_INVALID";

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
    public const string ReceiptExceedsPurchase = "RECEIPT_EXCEEDS_PURCHASE";
    public const string FgHandoverExceedsQc = "FG_HANDOVER_EXCEEDS_QC";
    public const string DispatchExceedsOrder = "DISPATCH_EXCEEDS_ORDER";
    public const string AnnouncementInvalid = "ANNOUNCEMENT_INVALID";
    public const string GradeInvalid = "GRADE_INVALID";
    public const string EmployeeImportInvalid = "EMPLOYEE_IMPORT_INVALID";
    public const string VehiclePlateDuplicate = "VEHICLE_PLATE_DUPLICATE";
    public const string VehicleInactive = "VEHICLE_INACTIVE";
    public const string VehicleTripInvalid = "VEHICLE_TRIP_INVALID";
    public const string ReturnExceedsIssued = "RETURN_EXCEEDS_ISSUED";
    public const string AllocationExceedsRequired = "ALLOCATION_EXCEEDS_REQUIRED";
    public const string PieceHandoverExceedsHolding = "PIECE_HANDOVER_EXCEEDS_HOLDING";
    public const string PieceRetrievalExceedsHolding = "PIECE_RETRIEVAL_EXCEEDS_HOLDING";
    public const string ShortageNotResolvable = "SHORTAGE_NOT_RESOLVABLE";
    public const string ShortageExceeded = "SHORTAGE_EXCEEDED";

    // Nhà cung cấp, đơn mua, thanh toán (Minh, MI-07)
    public const string SupplierNameDuplicate = "SUPPLIER_NAME_DUPLICATE";
    public const string SupplierNotActive = "SUPPLIER_NOT_ACTIVE";
    public const string PurchaseOrderInvalidStatus = "PURCHASE_ORDER_INVALID_STATUS";
    public const string PurchaseOrderLinesMismatch = "PURCHASE_ORDER_LINES_MISMATCH";
    public const string PurchaseOrderPriceRequired = "PURCHASE_ORDER_PRICE_REQUIRED";
    public const string PurchaseOrderDateInvalid = "PURCHASE_ORDER_DATE_INVALID";
    public const string PaymentInvalid = "PAYMENT_INVALID";

    // Công đoạn, đơn giá, phân công, sản lượng, QC, hàng hỏng (KI-01…KI-11, Minh làm hộ Kiều)
    public const string ProductionForbidden = "PRODUCTION_FORBIDDEN";
    public const string OperationCodeDuplicate = "OPERATION_CODE_DUPLICATE";
    public const string OperationInvalid = "OPERATION_INVALID";
    public const string OperationFinalStepExists = "OPERATION_FINAL_STEP_EXISTS";
    public const string OperationNotActive = "OPERATION_NOT_ACTIVE";
    public const string OperationProductMismatch = "OPERATION_PRODUCT_MISMATCH";
    public const string OperationExcelInvalid = "OPERATION_EXCEL_INVALID";
    public const string OperationExcelEmpty = "OPERATION_EXCEL_EMPTY";
    public const string OperationImportSheetNotFound = "OPERATION_IMPORT_SHEET_NOT_FOUND";
    public const string OperationImportFinalNotFound = "OPERATION_IMPORT_FINAL_NOT_FOUND";
    public const string ReplacementCuttingInvalid = "REPLACEMENT_CUTTING_INVALID";
    public const string PieceRateInvalidStatus = "PIECE_RATE_INVALID_STATUS";
    public const string PieceRateDuplicateDate = "PIECE_RATE_DUPLICATE_DATE";
    public const string PieceRateNotApproved = "PIECE_RATE_NOT_APPROVED";
    public const string AssignmentWorkerInvalid = "ASSIGNMENT_WORKER_INVALID";
    public const string AssignmentOrderInvalid = "ASSIGNMENT_ORDER_INVALID";
    public const string OutputInvalid = "OUTPUT_INVALID";
    public const string OutputExceedsCap = "OUTPUT_EXCEEDS_CAP";
    public const string ProductionEditDeadlinePassed = "PRODUCTION_EDIT_DEADLINE_PASSED";
    public const string ProductionPeriodLocked = "PRODUCTION_PERIOD_LOCKED";
    public const string QcExceedsOnLine = "QC_EXCEEDS_ON_LINE";
    public const string QcBatchDuplicate = "QC_BATCH_DUPLICATE";
    public const string DefectInvalid = "DEFECT_INVALID";
    public const string DefectInvalidStatus = "DEFECT_INVALID_STATUS";
}
