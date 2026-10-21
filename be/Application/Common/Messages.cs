namespace NX_MTD.Application.Common;

/// <summary>Chuỗi tiếng Việt theo mã MSG trong SRS.</summary>
public static class Messages
{
    public const string InvalidCredentials = "Tên đăng nhập hoặc mật khẩu không đúng.";       // MSG04
    public const string AccountLocked = "Tài khoản đã bị khóa. Vui lòng liên hệ Admin.";     // MSG06
    public const string AccountDisabled = "Nhân viên đã nghỉ việc, không thể đăng nhập.";
    public const string InvalidRefreshToken = "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
    public const string UsernameRequired = "Vui lòng nhập tên đăng nhập.";                   // MSG01
    public const string PasswordRequired = "Vui lòng nhập mật khẩu.";                         // MSG01
    public const string Unauthorized = "Bạn chưa đăng nhập hoặc phiên đã hết hạn.";
    public const string Forbidden = "Bạn không có quyền thực hiện thao tác này.";
    public const string InternalError = "Lỗi hệ thống. Vui lòng thử lại.";
    public const string ConcurrencyConflict = "Dữ liệu đã bị người khác thay đổi. Vui lòng tải lại.";
    public const string LoggedOut = "Đã đăng xuất.";

    // --- Chấm công & suất ăn (Minh) ---
    public const string AttendanceEmployeeNotFound = "Không tìm thấy nhân viên.";
    public const string AttendanceCodeAlreadyMapped = "Mã chấm công này đã gán cho nhân viên khác.";
    public const string AttendanceEmployeeHasCode = "Nhân viên này đã có mã chấm công.";
    public const string AttendanceCodeNotFound = "Không có lượt quẹt nào mang mã chấm công này.";
    public const string AttendanceEmptyBatch = "Lô dữ liệu chấm công rỗng.";
    public const string AttendanceSyncOverdue =
        "Chưa đồng bộ được chấm công hơn 30 phút. Có thể nhập file dự phòng.";                // MSG23
    public const string DateRangeInvalid = "Ngày kết thúc phải không nhỏ hơn ngày bắt đầu.";
    public const string DateRangeTooWide = "Khoảng ngày tối đa là {0} ngày.";
    public const string AttendanceAdjustmentExists = "Nhân viên này đã có chấm bù cho ngày này.";
    public const string AttendanceEmployeeInactive = "Nhân viên đã nghỉ việc, không thể chấm bù.";
    public const string AttendanceAdjustmentNoTime = "Vui lòng nhập giờ vào hoặc giờ ra.";
    public const string AttendanceAdjustmentRequestRequired = "Chấm bù theo đơn phải chọn đơn xin chấm công.";
    public const string AttendanceRequestInvalid = "Đơn xin chấm công bù phải ở trạng thái đã duyệt.";
    public const string AttendancePeriodLockedMessage = "Tháng {0} đã khóa, không thể thay đổi dữ liệu.";   // MSG21
    public const string AttendanceExcelInvalid = "File Excel không đúng định dạng (cần cột: mã chấm công, ngày, giờ vào, giờ ra).";
    public const string AttendanceExcelEmpty = "File Excel không có dòng dữ liệu nào.";

    // ---- Chung ----
    public const string Saved = "Đã lưu.";                                                      // MSG03
    public const string NoData = "Không có dữ liệu phù hợp.";                                   // MSG02
    public const string NotFound = "Không tìm thấy dữ liệu hoặc bạn không có quyền xem.";
    public const string ReasonRequired = "Vui lòng nhập lý do.";                                // MSG09
    public const string Submitted = "Đã gửi, đang chờ xác nhận.";                               // MSG10
    public const string Confirmed = "Đã xác nhận.";                                             // MSG11
    public const string Returned = "Đã trả lại kèm lý do.";                                     // MSG12
    public const string Duplicate = "Dữ liệu bị trùng với bản ghi đã có. Vui lòng kiểm tra lại.";
    public const string ConstraintViolation = "Không lưu được: dữ liệu đang được dùng ở nơi khác hoặc giá trị không hợp lệ.";
    public static string Required(string field) => $"Vui lòng nhập {field}.";                  // MSG01

    // ---- Đính kèm ----
    public const string AttachmentOwnerTypeInvalid = "Loại chứng từ đính kèm không hợp lệ.";
    public const string AttachmentPurposeInvalid = "Loại ảnh không hợp lệ.";
    public const string AttachmentFileEmpty = "Vui lòng chọn ảnh.";
    public const string AttachmentFileTooLarge = "Ảnh vượt quá dung lượng cho phép ({0} MB).";
    public const string AttachmentFileTypeNotAllowed = "Chỉ nhận ảnh JPG, PNG hoặc WEBP.";
    public const string AttachmentLimitReached = "Đã đủ {0} ảnh, không thể thêm.";
    public const string AttachmentUploaded = "Đã tải ảnh lên.";
    public const string AttachmentRemoved = "Đã gỡ ảnh.";

    // ---- Đơn từ ----
    public const string RequestTypeNotAllowed = "Bạn không được tạo loại đơn này.";
    public const string RequestAlreadyDecided = "Đơn đã được xử lý, không thể thay đổi.";
    public const string RequestNotEditable = "Chỉ sửa hoặc hủy được đơn đang chờ.";
    public const string RequestManualCompleteNotAllowed = "Loại đơn này tự hoàn thành khi xong bước tiếp theo, không bấm tay.";
    public const string RequestCancelled = "Đã hủy đơn.";
    public const string RequestCompleted = "Đã hoàn thành đơn.";
    public const string RequestContentRequired = "Vui lòng nhập nội dung đơn.";
    public const string NotifyNewRequestTitle = "Có đơn mới cần xử lý";
    public const string NotifyNewRequestBody = "Đơn {0} ({1}) đang chờ bạn xử lý.";
    public const string NotifyRequestChangedTitle = "Đơn của bạn đã được cập nhật";
    public const string NotifyRequestChangedBody = "Đơn {0}: {1}.";

    // ---- Mã hàng, đơn hàng ----
    public const string ProductCodeDuplicate = "Mã hàng đã tồn tại.";
    public const string ProductCodeLocked = "Mã hàng đã dùng trong đơn hàng, không đổi được mã.";
    public const string ProductNotActive = "Mã hàng {0} đã ngừng dùng.";
    public const string ProductInvalidStatus = "Trạng thái mã hàng không cho phép thao tác này.";
    public const string OrderCodeDuplicate = "Mã đơn hàng đã tồn tại.";
    public const string OrderNotEditable = "Đơn hàng ở trạng thái này không sửa được.";
    public const string OrderLineRemoveNotAllowed = "Đơn đã vào sản xuất, không xóa được dòng đơn.";
    public const string OrderLineKeyLocked = "Đơn đã vào sản xuất, không đổi mã hàng / màu / size của dòng cũ.";
    public const string OrderInvalidStatus = "Trạng thái đơn hàng không cho phép thao tác này.";
    public const string OrderCancelled = "Đã hủy đơn hàng.";
    public const string OrderStopped = "Đã dừng đơn hàng.";
    public const string OrderHeaderLocked = "Đơn đã vào sản xuất, chỉ sửa ngày giao, ghi chú, số lượng, đơn giá và thêm dòng.";
    public const string NotifyNewOrderTitle = "Có đơn hàng mới";
    public const string NotifyNewOrderBody = "Đơn {0}, giao ngày {1}. Vui lòng xem NVL của đơn.";
    public const string CustomerNotFound = "Khách hàng không tồn tại.";
    public const string CustomerNameDuplicate = "Tên khách hàng đã tồn tại.";
    public const string ProductNotFound = "Mã hàng không tồn tại.";

    // ---- Nhân sự, tài khoản, tổ ----
    public const string PasswordMismatch = "Mật khẩu nhập lại không khớp.";                      // MSG05
    public const string WrongCurrentPassword = "Mật khẩu hiện tại không đúng.";                  // MSG07
    public const string PasswordChanged = "Đã đổi mật khẩu. Các thiết bị khác đã được đăng xuất."; // MSG08
    public const string EmployeeCodeDuplicate = "Mã nhân viên đã tồn tại.";
    public const string EmployeeAttendanceCodeTaken = "Mã chấm công {0} đã gán cho nhân viên khác.";
    public const string EmployeeNotActive = "Nhân viên đã nghỉ việc.";
    public const string EmployeeCreated = "Đã thêm nhân viên và tạo tài khoản.";
    public const string EmployeeResigned = "Đã cho nhân viên nghỉ việc.";
    public const string AccountCannotLockSelf = "Không thể tự khóa tài khoản của mình.";
    public const string AccountInvalidStatus = "Trạng thái tài khoản không cho phép thao tác này.";
    public const string AccountLockedDone = "Đã khóa tài khoản.";
    public const string AccountUnlockedDone = "Đã mở khóa tài khoản.";
    public const string PasswordResetDone = "Đã đặt lại mật khẩu tạm.";
    public const string TeamCodeDuplicate = "Mã tổ đã tồn tại.";
    public const string TeamNameDuplicate = "Tên tổ đã tồn tại.";
    public const string TeamNotActive = "Tổ đã ngừng hoạt động.";
    public const string TeamLeaderInvalidTeam = "Tổ trưởng / tổ phó phải thuộc tổ cắt hoặc tổ may đang hoạt động.";
    public const string TeamLeaderExistsNamed = "Tổ {0} đã có tổ trưởng: {1}.";
    public const string TeamDeputyExistsNamed = "Tổ {0} đã có tổ phó: {1}.";
    public const string TeamHasActiveMembers = "Tổ còn {0} nhân viên đang làm, chuyển hết trước khi ngừng / đổi loại tổ.";
    public const string TeamTransferFutureDate = "Ngày hiệu lực không được sau hôm nay.";
    public const string TeamTransferLeader = "{0} đang là tổ trưởng / tổ phó. Bổ nhiệm người khác trước khi chuyển tổ.";
    public const string TeamMemberNotInTeam = "Nhân viên không thuộc tổ này.";
    public const string TeamAppointed = "Đã bổ nhiệm. Người giữ chức cũ (nếu có) đã chuyển về Công nhân.";
    public const string TeamTransferred = "Đã chuyển {0} nhân viên.";
    public const string TeamAppointInvalidRole = "Chỉ bổ nhiệm được công nhân, tổ trưởng, tổ phó của tổ.";
    public const string CannotActOnSelf = "Không thể tự thao tác trên tài khoản của mình.";
    public const string ResignedWasLeader = "Đã cho nghỉ. Tổ {0} hiện chưa có {1}, hãy bổ nhiệm người thay.";

    // --- Suất ăn (UC30) ---
    public const string MealTeamNotFound = "Không tìm thấy tổ.";
    public const string MealNotFound = "Không tìm thấy suất ăn ngày này.";
    public const string MealLocked =
        "Đã chốt suất ăn lúc 08:30, không thể thay đổi.";                                       // MSG22
    public const string MealGuestLocked =
        "Đã chốt suất ăn lúc 08:30, không thể thay đổi suất khách.";                             // MSG22
    public const string MealNotClosedYet = "Suất ăn ngày này chưa chốt.";
    public const string MealCountInvalid = "Số suất ăn phải lớn hơn hoặc bằng 0.";
    public const string MealPriceInvalid = "Đơn giá suất ăn phải lớn hơn hoặc bằng 0.";
    public const string MealKitchenNotified = "Đã báo bếp.";
    public const string NotifyMealReminderTitle = "Nhắc xác nhận suất ăn";
    public const string NotifyMealReminderBody =
        "Tổ {0} chưa xác nhận suất ăn ngày {1}. Hệ thống đang tạm lấy theo số người đã quẹt ({2}); vui lòng xác nhận trước 08:30.";

    // --- Bảng lương + khóa tháng (UC06, UC31, UC32) ---
    public const string PayrollNotFound = "Không tìm thấy bảng lương.";
    public const string PayrollEmpty = "Bảng lương chưa có phiếu lương nào, không thể gửi duyệt.";
    public const string PayrollInvalidStatusForImport =
        "Bảng lương đang chờ duyệt hoặc đã duyệt, không thể nhập lại. Vui lòng mở lại về Nháp trước.";
    public const string PayrollInvalidStatusForSubmit = "Chỉ bảng lương ở trạng thái Nháp mới gửi duyệt được.";
    public const string PayrollInvalidStatusForApprove = "Chỉ bảng lương đang chờ duyệt mới duyệt được.";
    public const string PayrollInvalidStatusForReject = "Chỉ bảng lương đang chờ duyệt mới từ chối được.";
    public const string PayrollInvalidStatusForReopen = "Chỉ bảng lương đã duyệt mới mở lại được.";
    public const string PayrollRejectReasonRequired = "Vui lòng nhập lý do từ chối.";          // MSG09
    public const string PayrollReopenReasonRequired = "Vui lòng nhập lý do mở lại.";            // MSG09
    public const string PayrollExcelInvalid =
        "File Excel không đúng mẫu bảng thanh toán lương (cần sheet có cột SLĐ và cột Thực lĩnh/Còn lĩnh).";
    public const string PayrollFileNotFound = "Bảng lương này chưa có file gốc để tải.";
    public const string PayrollExcelEmpty = "File Excel không có dòng dữ liệu nào.";
    public const string PayrollPeriodMonthInvalid = "Tháng phải theo định dạng YYYY-MM.";
    public const string PayslipNotFound = "Chưa có phiếu lương đã duyệt cho tháng này.";
    public const string ExcelExported = "Đã xuất file Excel.";                                  // MSG27

    // --- Chi phí tháng (UC12, UC33) ---
    public const string MonthlyCostNotFound = "Chưa có dữ liệu chi phí tháng này.";
    public const string MonthlyCostPeriodMonthInvalid = "Tháng phải theo định dạng YYYY-MM.";
    public const string MonthlyCostExcelInvalid =
        "File Excel không đúng định dạng (cần cột: loại chi phí, kiểu, mã hàng, số tiền, số lượng, ghi chú).";
    public const string MonthlyCostExcelEmpty = "File Excel không có dòng dữ liệu nào.";

    // --- KPI tổ may (UC19 KPI tổ may — Quản đốc lập/sửa/hủy chỉ tiêu, khác UC19 lệnh cắt) ---
    public const string KpiNotFound = "Không tìm thấy chỉ tiêu KPI.";
    public const string KpiOrderNotFound = "Không tìm thấy đơn hàng.";
    public const string KpiTeamInvalid = "Tổ may không hợp lệ hoặc đã ngừng hoạt động.";
    public const string KpiProductNotInOrder = "Mã hàng không thuộc đơn hàng đã chọn.";
    public const string KpiDuplicatePeriod =
        "Tổ may này đã có chỉ tiêu KPI cho đơn hàng, mã hàng và ngày bắt đầu này.";
    public const string KpiInvalidStatusForEdit = "Chỉ có thể sửa chỉ tiêu KPI đang hoạt động (ACTIVE).";
    public const string KpiInvalidStatusForCancel = "Chỉ có thể hủy chỉ tiêu KPI đang hoạt động (ACTIVE).";
    public const string KpiTargetQtyMustBePositive = "Chỉ tiêu sản lượng phải lớn hơn 0.";
    public const string KpiWorkingDaysMustBePositive = "Số ngày làm việc phải lớn hơn 0.";

    // --- Doanh thu (UC27) + Hòa vốn, giá trị thặng dư (UC11, GBR-17, chỉ Admin) ---
    public const string RevenuePeriodMonthInvalid = "Tháng phải theo định dạng YYYY-MM.";
    public const string BreakEvenPeriodMonthInvalid = "Tháng phải theo định dạng YYYY-MM.";
    public const string BreakEvenInvalidAllocationBasis = "Cách phân bổ định phí không hợp lệ (cần OUTPUT hoặc LABOUR_HOURS).";
    /// <summary>MSG25.</summary>
    public const string BreakEvenNotReady = "Chưa thể tính: bảng lương tháng chưa được duyệt hoặc chi phí tháng chưa được nhập.";
    public const string BreakEvenNoOutput = "Tháng này chưa có mã hàng nào hoàn thiện, không có dữ liệu để tính hòa vốn.";
    public const string BreakEvenNotFound = "Chưa có dữ liệu hòa vốn cho tháng này.";
    /// <summary>MSG26 — dùng khi BreakEvenQty tính được (đơn giá &gt; biến phí/sp).</summary>
    public const string BreakEvenAtRiskWarning = "Mã hàng {0} có nguy cơ lỗ: mới đạt {1}% điểm hòa vốn.";
    /// <summary>Biến thể MSG26 khi BreakEvenQty = NULL (đơn giá ≤ biến phí/sp, không thể hòa vốn) — không có
    /// "%" để báo vì không tính được điểm hòa vốn (xem docs/CODE_DEVIATIONS.md).</summary>
    public const string BreakEvenAtRiskWarningNoBreakEven =
        "Mã hàng {0} có nguy cơ lỗ: không thể đạt điểm hòa vốn với đơn giá hiện tại.";
    public const string BreakEvenCalculated = "Đã tính hòa vốn tháng {0}.";

    // --- Báo cáo kế toán (UC36, CHỈ Kế toán) ---
    public const string AccountingReportPeriodMonthInvalid = "Tháng phải theo định dạng YYYY-MM.";

    // --- Nhà cung cấp + đơn mua + thanh toán (UC14, UC23, UC34) ---
    public const string SupplierNameDuplicate = "Tên nhà cung cấp đã tồn tại.";
    public const string SupplierNotActive = "Nhà cung cấp đã ngừng hợp tác, vui lòng chọn nhà cung cấp khác.";
    public const string SupplierNotFound = "Nhà cung cấp không tồn tại.";
    public const string SupplierSaved = "Đã lưu nhà cung cấp.";
    public const string SupplierDiscontinued = "Đã ngừng hợp tác với nhà cung cấp.";
    public const string PurchaseOrderInvalidStatus = "Trạng thái đơn mua không cho phép thao tác này.";
    public const string PurchaseOrderLinesMismatch = "Danh sách dòng phải đủ mọi dòng của đơn mua, không thêm hoặc bớt.";
    public const string PurchaseOrderPriceRequired = "Vui lòng nhập đơn giá lớn hơn 0 cho mọi dòng trước khi đặt hàng.";
    public const string PurchaseOrderQtyLocked = "Đơn đã đặt hàng, không đổi được số lượng hoặc nhà cung cấp.";
    public const string PurchaseOrderOrderDateFuture = "Ngày đặt hàng không được sau hôm nay.";
    public const string PurchaseOrderDeliveryBeforeOrder = "Ngày giao dự kiến không được trước ngày đặt hàng.";
    public const string PurchaseOrderPlaced = "Đã đặt hàng. Kế toán sẽ xác nhận thanh toán.";
    public const string PurchaseOrderCancelled = "Đã hủy đơn mua.";
    public const string PurchaseOrderRepriced = "Đã cập nhật đơn giá và gửi lại Kế toán.";
    public const string PaymentConfirmed = "Đã xác nhận thanh toán đơn mua.";
    public const string PaymentReturned = "Đã trả lại Quản đốc kèm lý do.";
    public const string PaymentMethodInvalid = "Hình thức thanh toán phải là chuyển khoản hoặc tiền mặt.";
    public const string PaymentAmountInvalid = "Số tiền thanh toán phải lớn hơn 0.";
    public const string PaymentDateFuture = "Ngày thanh toán không được sau hôm nay.";
    public const string NotifyPoPlacedTitle = "Đơn mua chờ thanh toán";
    public const string NotifyPoPlacedBody = "Đơn mua {0} (NCC {1}) đã đặt hàng, chờ xác nhận thanh toán.";
    public const string NotifyPoPaidTitle = "Đơn mua đã thanh toán";
    public const string NotifyPoPaidBody = "Đơn mua {0} đã được Kế toán xác nhận thanh toán.";
    public const string NotifyPoReturnedTitle = "Đơn mua bị trả lại";
    public const string NotifyPoReturnedBody = "Kế toán trả lại đơn mua {0}: {1}";
    public const string NotifyPoRepricedTitle = "Đơn mua chờ thanh toán lại";
    public const string NotifyPoRepricedBody = "Đơn mua {0} đã được Quản đốc cập nhật đơn giá.";
}
