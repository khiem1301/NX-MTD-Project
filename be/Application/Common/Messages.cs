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
    public const string EmployeeImportSummary = "{0} dòng hợp lệ, {1} dòng lỗi, {2} dòng trùng. Chỉ các dòng hợp lệ được nhập.";
    public const string EmployeeImportDone = "Đã nhập {0} nhân viên, {1} dòng không nhập được.";
    public const string EmployeeImportEmpty = "File không có dòng nhân viên nào.";
    public const string EmployeeImportTooMany = "File tối đa {0} dòng.";
    public const string EmployeeImportExists = "Mã nhân viên đã có trong hệ thống.";
    public const string EmployeeImportRepeated = "Mã nhân viên bị lặp trong file.";
    public const string EmployeeImportAttendanceRepeated = "Mã chấm công bị lặp trong file.";
    public const string EmployeeImportTeamUnknown = "Không tìm thấy tổ có mã {0}.";
    public const string GradeCodeInvalid = "Bậc thợ phải là A, B, C hoặc D.";
    public const string GradeLevelDuplicate = "Bậc này đã có mức tiền cho ngày hiệu lực đó.";
    public const string GradeWorkerNotInTeam = "Chỉ đề xuất bậc cho công nhân đang làm việc trong tổ của bạn.";
    public const string GradeSameAsCurrent = "Bậc đề xuất trùng bậc hiện tại.";
    public const string GradePendingExists = "Công nhân này đã có đề xuất đang chờ quyết định.";
    public const string GradeProposedToday = "Hôm nay bạn đã đề xuất bậc cho công nhân này.";
    public const string GradeProposed = "Đã gửi đề xuất bậc thợ.";
    public const string GradeAlreadyDecided = "Đề xuất đã được quyết định, không đổi được (GBR-22).";
    public const string ManagerCannotAssignRole = "Quản đốc không gán được vai trò Admin, Quản đốc hoặc Kế toán.";
    public const string GradeWorkerInvalid = "Chỉ đặt bậc cho công nhân đang làm việc.";
    public const string GradeSupersededNote = "Quản đốc đã đặt bậc trực tiếp nên đề xuất này không còn hiệu lực.";
    public const string GradeSetToday = "Hôm nay bạn đã đặt bậc cho công nhân này.";
    public const string GradeSet = "Đã đặt bậc thợ.";
    public const string GradeApplied = "Đã áp dụng bậc thợ.";
    public const string GradeNotApplied = "Đã ghi nhận không áp dụng bậc thợ.";
    public const string AnnouncementTooLong = "Nội dung thông báo tối đa 1000 ký tự.";
    public const string AnnouncementTargetRequired = "Vui lòng chọn ít nhất một tổ hoặc một vai trò nhận thông báo.";
    public const string AnnouncementScopeInvalid = "Phạm vi gửi phải là toàn xưởng, theo tổ hoặc theo vai trò.";
    public const string AnnouncementNoRecipient = "Không có người nhận nào trong phạm vi đã chọn.";
    public const string AnnouncementSent = "Đã gửi thông báo cho {0} người.";
    public const string OrderStarted = "Đã chuyển đơn hàng sang sản xuất.";
    public const string OrderMaterialsAwaiting = "Đơn chưa nhận NVL nào (đang chờ NVL), chưa thể chuyển sang sản xuất.";
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
    public const string KpiExceedsStock = "Chỉ tiêu vượt BTP trong kho: BTP đã cắt vào kho {0}, các chỉ tiêu khác đã đặt {1}, còn đặt được {2}.";
    public const string ProductSpecComponentDuplicate = "Tên thành phần của mã hàng bị trùng.";
    public const string ProductSpecVariantDuplicate = "Màu – size của mã hàng bị trùng.";
    public const string ProductSpecNormReference = "Định mức phải dùng thành phần và màu – size có trong danh sách.";
    public const string ProductSpecNormDuplicate = "Mỗi NVL chỉ khai một dòng cho mỗi thành phần và màu – size.";
    public const string ProductSpecMaterialInvalid = "NVL trong định mức không tồn tại hoặc đã ngừng dùng.";
    public const string ProductVariantInactive = "Màu – size {0} của mã hàng {1} đã ngừng dùng.";
    public const string PurchaseOrderOrderDateFuture = "Ngày đặt hàng không được sau hôm nay.";
    public const string PurchaseOrderDeliveryCountInvalid = "Số lần nhà cung cấp dự kiến giao phải từ 1 trở lên.";
    public const string PurchaseOrderPlaced = "Đã đặt hàng. Kế toán sẽ xác nhận thanh toán.";
    public const string PurchaseOrderCancelled = "Đã hủy đơn mua.";
    public const string PurchaseOrderRepriced = "Đã cập nhật đơn giá và gửi lại Kế toán.";
    public const string PaymentConfirmed = "Đã xác nhận thanh toán đơn mua.";
    public const string PaymentReturned = "Đã trả lại Quản đốc kèm lý do.";
    public const string PaymentMethodInvalid = "Hình thức thanh toán phải là chuyển khoản hoặc tiền mặt.";
    public const string PaymentAmountInvalid = "Số tiền thanh toán phải lớn hơn 0.";
    public const string PaymentDateFuture = "Ngày thanh toán không được sau hôm nay.";
    public const string NotifyIncomingCustomerTitle = "Khách sẽ cấp NVL";
    public const string NotifyIncomingCustomerBody = "Đơn {0}: khách cấp {1} {2} {3}, dự kiến giao {4} lần. Xem màn NVL chờ nhận để lập phiếu nhập khi hàng về.";
    public const string NotifyIncomingPoTitle = "Có đơn mua NVL chờ nhận";
    public const string NotifyIncomingPoBody = "Đơn mua {0} (NCC {1}) đã đặt hàng. Xem màn NVL chờ nhận để lập phiếu nhập khi hàng về.";
    public const string NotifyPoPlacedTitle = "Đơn mua chờ thanh toán";
    public const string NotifyPoPlacedBody = "Đơn mua {0} (NCC {1}) đã đặt hàng, chờ xác nhận thanh toán.";
    public const string NotifyPoPaidTitle = "Đơn mua đã thanh toán";
    public const string NotifyPoPaidBody = "Đơn mua {0} đã được Kế toán xác nhận thanh toán.";
    public const string NotifyPoReturnedTitle = "Đơn mua bị trả lại";
    public const string NotifyPoReturnedBody = "Kế toán trả lại đơn mua {0}: {1}";
    public const string NotifyPoRepricedTitle = "Đơn mua chờ thanh toán lại";
    public const string NotifyPoRepricedBody = "Đơn mua {0} đã được Quản đốc cập nhật đơn giá.";

    // --- Công đoạn + đơn giá (UC25, UC26), phân công (UC52), sản lượng (UC55), QC (UC57), hàng hỏng (UC56) ---
    public const string ProductionSewingTeamOnly = "Chức năng này chỉ dành cho tổ trưởng / tổ phó tổ may.";
    public const string OperationCodeDuplicate = "Mã công đoạn đã tồn tại trong mã hàng này.";
    public const string OperationFinalStepExists = "Mã hàng này đã có công đoạn may cuối đang dùng. Chỉ được một công đoạn cuối.";
    public const string OperationNotActive = "Công đoạn đã ngừng dùng.";
    public const string OperationProductMismatch = "Công đoạn không thuộc mã hàng của dòng đơn này.";
    public const string OperationExcelInvalid = "File Excel sai mẫu: không đọc được bảng bước công việc.";
    public const string OperationExcelEmpty = "File Excel không có công đoạn hợp lệ nào để nhập.";
    public const string OperationImportSheetNotFound = "Không tìm thấy sheet đã chọn trong file Excel.";
    public const string OperationImportDone =
        "Đã nhập công đoạn: thêm {0}, cập nhật {1}, không đổi {2}, bỏ qua {3} dòng lỗi. Đơn giá: thêm {4}, cập nhật {5}, không đổi {6}, bỏ qua {7}.";
    public const string NotifyPieceRatesImportedBody = "Sheet {0}: có {1} đơn giá công đoạn mới nhập từ Excel đang chờ duyệt.";
    public const string PieceRatesApprovedMany = "Đã duyệt {0} đơn giá.";
    public const string PieceRateNoneSelected = "Chưa chọn đơn giá nào.";
    public const string NotifyPieceRatesApprovedManyBody = "{0} đơn giá công đoạn đã được duyệt.";
    public const string OperationImportFinalNotFound = "Số thứ tự công đoạn cuối không có trong các dòng hợp lệ của file.";
    public const string ReplacementCuttingNeedsDefect = "Đơn cắt bù phải gắn với một phiếu hàng hỏng.";
    public const string ReplacementCuttingDefectCancelled = "Phiếu hàng hỏng này đã bị hủy.";
    public const string ReplacementCuttingDefectNotScrap = "Chỉ phiếu hàng hỏng loại Hủy (Scrap) mới cần cắt bù.";
    public const string ReplacementCuttingOtherTeam = "Phiếu hàng hỏng này không thuộc tổ của bạn.";
    public const string ReplacementCuttingOrderMismatch = "Đơn hàng của đơn cắt bù phải là đơn hàng của phiếu hàng hỏng.";
    public const string ReplacementCuttingLines = "Mỗi dòng cắt bù cần có chi tiết cần cắt (không trùng nhau), đúng dòng đơn của phiếu hàng hỏng và số lượng từ 1 đến số hàng đã hủy.";
    public const string ReplacementCuttingDuplicate = "Phiếu hàng hỏng này đã có đơn cắt bù.";
    public const string PieceRateInvalidStatus = "Trạng thái đơn giá không cho phép thao tác này.";
    public const string PieceRateDuplicateDate = "Công đoạn này đã có đơn giá với ngày hiệu lực này.";
    public const string PieceRateSubmitted = "Đã gửi duyệt đơn giá.";
    public const string PieceRateApproved = "Đã duyệt đơn giá.";
    public const string PieceRateRejected = "Đã từ chối đơn giá.";
    /// <summary>MSG18 (GBR-11): chưa có đơn giá được duyệt thì không nhập sản lượng.</summary>
    public const string PieceRateNotApproved = "Công đoạn {0} chưa có đơn giá được duyệt hiệu lực đến ngày {1}, chưa nhập được sản lượng.";
    public const string NotifyPieceRateSubmittedTitle = "Đơn giá chờ duyệt";
    public const string NotifyPieceRateSubmittedBody = "Công đoạn {0} ({1}) có đơn giá mới chờ duyệt.";
    public const string NotifyPieceRateDecidedTitle = "Đơn giá đã được xử lý";
    public const string NotifyPieceRateDecidedBody = "Đơn giá công đoạn {0}: {1}.";
    public const string AssignmentWorkerInvalid = "Công nhân phải là người đang làm của tổ mình.";
    public const string AssignmentOrderInvalid = "Đơn hàng không tồn tại hoặc đã đóng.";
    public const string AssignmentNotFound = "Không tìm thấy phân công đang hiệu lực.";
    public const string AssignmentSaved = "Đã lưu phân công.";
    public const string AssignmentOperationsRequired = "Chọn ít nhất một công đoạn.";
    public const string AssignmentOperationsDuplicate = "Công đoạn bị chọn trùng.";
    public const string AssignmentClosed = "Đã kết thúc phân công.";
    public const string AssignmentCopied = "Đã chép {0} phân công.";
    public const string AssignmentCopySameOrder = "Đơn nguồn và đơn đích phải khác nhau.";
    public const string OutputDateFuture = "Không nhập sản lượng cho ngày chưa tới.";
    public const string OutputQtyInvalid = "Số lượng phải là số nguyên không âm.";
    public const string OutputRowsRequired = "Chưa có dòng sản lượng nào để lưu.";
    public const string OutputDuplicateRow = "Công đoạn và công nhân bị lặp trong danh sách.";
    public const string OutputTeamRequired = "Quản đốc phải chọn tổ may.";
    /// <summary>MSG16: vượt số BTP tổ / công nhân đã nhận.</summary>
    public const string OutputExceedsCap = "Sản lượng công đoạn {0} vượt số BTP đã nhận ({1}): còn tối đa {2}.";
    /// <summary>MSG17: công nhân chưa có chấm công hôm đó (cảnh báo, không chặn).</summary>
    public const string OutputNoAttendanceWarning = "{0} chưa có giờ chấm công ngày {1}.";
    public const string OutputSaved = "Đã lưu sản lượng.";
    /// <summary>MSG20: quá hạn sửa của tổ trưởng / QA-QC, chỉ Quản đốc sửa có lý do.</summary>
    public const string ProductionEditDeadlinePassed = "Đã quá hạn sửa. Chỉ Quản đốc được sửa và phải nhập lý do.";
    public const string ProductionPeriodLocked = "Tháng {0} đã khóa, không thể thay đổi sản lượng.";
    public const string QcDateFuture = "Không nhập QC cho ngày chưa tới.";
    /// <summary>MSG19: QC vượt số hàng đang có trên chuyền.</summary>
    public const string QcExceedsOnLine = "Số đạt vượt hàng đang có trên chuyền ({0}).";
    public const string QcBatchDuplicate = "Đã có đợt QC cùng ca, giờ, tổ và dòng đơn.";
    public const string QcBatchSaved = "Đã lưu đợt QC.";
    public const string QcBatchDeleted = "Đã xóa đợt QC.";
    public const string DefectReasonInvalid = "Lý do lỗi không hợp lệ.";
    public const string DefectTypeInvalid = "Loại lỗi phải là Rework hoặc Scrap.";
    public const string DefectNoteRequired = "Lý do 'Khác' bắt buộc ghi chú.";
    public const string DefectInvalidStatus = "Phiếu lỗi này không còn hiệu lực để thao tác.";
    public const string DefectSaved = "Đã ghi phiếu lỗi.";
    public const string DefectCancelled = "Đã hủy phiếu lỗi.";
    public const string DefectObjected = "Đã gửi phản hồi cho tổ trưởng.";
    public const string DefectObjectionRequired = "Vui lòng nhập nội dung phản hồi.";
    public const string NotifyDefectTitle = "Bạn có phiếu lỗi mới";
    public const string NotifyDefectBody = "Ngày {0}: {1} cái {2} ở công đoạn {3}. Nếu ghi sai, hãy phản hồi cho tổ trưởng.";
    public const string NotifyDefectObjectionTitle = "Công nhân phản hồi phiếu lỗi";
    public const string NotifyDefectObjectionBody = "{0} phản hồi phiếu {1}: {2}";
}
