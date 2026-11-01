import { formatCurrency, formatDate, formatDateTime, formatQty } from '@/utils/format';

/** Tên đối tượng (bảng DB) hiển thị cho người dùng trong nhật ký thay đổi. */
export const AUDIT_TABLE_LABEL: Record<string, string> = {
  Account: 'Tài khoản',
  AccountingPeriod: 'Kỳ kế toán',
  Announcement: 'Thông báo chung',
  BreakEvenResult: 'Kết quả hòa vốn',
  Customer: 'Khách hàng',
  CustomerOrder: 'Đơn hàng',
  DefectRecord: 'Phiếu lỗi',
  DispatchNote: 'Phiếu xuất giao',
  Employee: 'Nhân viên',
  GradeLevel: 'Mức tiền bậc thợ',
  GradeLevelCode: 'Bậc thợ',
  KpiTarget: 'Chỉ tiêu KPI',
  MonthlyCost: 'Chi phí tháng',
  Operation: 'Công đoạn',
  OutputRecord: 'Sản lượng',
  Payroll: 'Bảng lương',
  PieceRate: 'Đơn giá công đoạn',
  Product: 'Mã hàng',
  PurchaseOrder: 'Đơn mua NVL',
  QcBatch: 'Lô QC',
  Request: 'Đơn từ',
  Stocktake: 'Phiếu kiểm kho',
  Supplier: 'Nhà cung cấp',
  SystemSetting: 'Cài đặt hệ thống',
  Team: 'Tổ',
  Vehicle: 'Xe',
  VehicleTrip: 'Chuyến xe',
  WorkAllocation: 'Chia việc',
};

export const auditTableLabel = (name: string): string => AUDIT_TABLE_LABEL[name] ?? name;

/** Hành động ghi nhật ký: nhãn tiếng Việt và màu thẻ. */
export const AUDIT_ACTION: Record<string, { text: string; color: string }> = {
  INSERT: { text: 'Tạo mới', color: 'green' },
  UPDATE: { text: 'Cập nhật', color: 'blue' },
  DELETE: { text: 'Xóa', color: 'red' },
  APPROVE: { text: 'Duyệt', color: 'cyan' },
  REJECT: { text: 'Từ chối', color: 'orange' },
  CONFIRM: { text: 'Xác nhận', color: 'geekblue' },
  LOCK: { text: 'Khóa', color: 'purple' },
  UNLOCK: { text: 'Mở khóa', color: 'purple' },
  LOGIN: { text: 'Đăng nhập', color: 'default' },
};

export const auditActionText = (action: string): string => AUDIT_ACTION[action]?.text ?? action;

const FIELD_LABEL: Record<string, string> = {
  Account: 'Tài khoản',
  ActualQty: 'Số lượng thực tế',
  AdjustedLines: 'Số dòng điều chỉnh',
  AttendanceCode: 'Mã chấm công',
  Components: 'Số thành phần',
  ConfirmMode: 'Cách xác nhận',
  ConfirmedQty: 'Số lượng xác nhận',
  Content: 'Nội dung',
  Count: 'Số lượng',
  CurrentGradeCode: 'Bậc thợ hiện tại',
  CustomerId: 'Khách hàng',
  CustomerReceivedDate: 'Ngày khách nhận',
  DateOfBirth: 'Ngày sinh',
  DecisionNote: 'Ghi chú duyệt',
  DefectCode: 'Mã phiếu lỗi',
  DefectReasonCode: 'Nguyên nhân lỗi',
  DefectType: 'Loại lỗi',
  Description: 'Mô tả',
  DispatchCode: 'Mã phiếu xuất giao',
  DueDate: 'Ngày giao',
  EffectiveDate: 'Ngày hiệu lực',
  EmployeeCode: 'Mã nhân viên',
  ExpectedDeliveryCount: 'Số lần giao dự kiến',
  ExtraImages: 'Số ảnh phụ',
  FuelLiters: 'Số lít nhiên liệu',
  FuelOverNormWarning: 'Cảnh báo vượt định mức nhiên liệu',
  FullName: 'Họ tên',
  Gender: 'Giới tính',
  GoodsStatus: 'Trạng thái hàng',
  GradeCode: 'Mã bậc thợ',
  GradeName: 'Tên bậc thợ',
  IsFinalStep: 'Công đoạn cuối',
  ItemCount: 'Số khoản',
  KmGapWarning: 'Cảnh báo chênh số km',
  LineCount: 'Số dòng',
  Lines: 'Các dòng',
  MismatchRecords: 'Số phiếu lệch kiểm',
  MonthlyAmount: 'Số tiền theo tháng',
  Norms: 'Số dòng định mức',
  Note: 'Ghi chú',
  OdometerEnd: 'Số km cuối',
  OdometerStart: 'Số km đầu',
  OperationCode: 'Mã công đoạn',
  OperationId: 'Công đoạn',
  OperationName: 'Tên công đoạn',
  OrderCode: 'Mã đơn',
  OrderDate: 'Ngày đặt',
  OrderLineId: 'Dòng đơn',
  OrderLines: 'Các dòng đơn',
  PaidAmount: 'Số tiền đã trả',
  PasswordChanged: 'Đã đổi mật khẩu',
  PasswordReset: 'Đã đặt lại mật khẩu',
  PayType: 'Hình thức lương',
  PaymentDate: 'Ngày thanh toán',
  PaymentMethod: 'Hình thức thanh toán',
  PaymentNote: 'Ghi chú thanh toán',
  PaymentStatus: 'Trạng thái thanh toán',
  PayslipCount: 'Số phiếu lương',
  PeriodMonth: 'Tháng',
  PieceRateId: 'Đơn giá',
  PlateNumber: 'Biển số',
  Phone: 'Số điện thoại',
  Priority: 'Mức ưu tiên',
  ProductCode: 'Mã hàng',
  ProductId: 'Mã hàng',
  ProductName: 'Tên hàng',
  ProposedGradeCode: 'Bậc thợ đề xuất',
  PurchaseOrderLines: 'Các dòng đơn mua',
  Qty: 'Số lượng',
  Quantity: 'Số lượng',
  Rate: 'Đơn giá',
  ReasonNote: 'Ghi chú nguyên nhân',
  ReceivedDate: 'Ngày nhận',
  Recipients: 'Số người nhận',
  ResignDate: 'Ngày nghỉ việc',
  ResponsibleWorkerId: 'Công nhân chịu trách nhiệm',
  Result: 'Kết quả',
  ResultNote: 'Ghi chú kết quả',
  RoleCode: 'Vai trò',
  SampleImageUrl: 'Ảnh chính',
  SewingTeamId: 'Tổ may',
  ShiftCode: 'Ca làm việc',
  ShortageResolution: 'Hướng xử lý thiếu',
  SkillLevel: 'Bậc tay nghề',
  Spec: 'Định mức',
  StandardTimeSec: 'Thời gian chuẩn (giây)',
  StartDate: 'Ngày vào làm',
  StationSequence: 'Thứ tự',
  Status: 'Trạng thái',
  StopReason: 'Lý do dừng',
  SupplierId: 'Nhà cung cấp',
  TargetDate: 'Ngày mục tiêu',
  TargetScope: 'Phạm vi nhận',
  TeamId: 'Tổ',
  TeamType: 'Loại tổ',
  Title: 'Tiêu đề',
  TotalAmount: 'Tổng tiền',
  TripDate: 'Ngày đi',
  UsesSemiFinished: 'Dùng bán thành phẩm',
  Variants: 'Số biến thể',
  VehicleId: 'Xe',
  VehicleType: 'Loại xe',
  WorkDate: 'Ngày làm việc',
  WorkerId: 'Công nhân',
  WorkerObjection: 'Công nhân phản đối',
};

/** Tên trường của log: có trong từ điển thì dùng nhãn tiếng Việt, không thì tách chữ hoa (CurrentGrade → Current grade). */
export const auditFieldLabel = (key: string): string =>
  FIELD_LABEL[key] ??
  key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase());

const VALUE_LABEL: Record<string, string> = {
  ACTIVE: 'Đang hoạt động',
  Active: 'Đang hoạt động',
  APPROVED: 'Đã duyệt',
  Approved: 'Đã duyệt',
  CANCELLED: 'Đã hủy',
  Cancelled: 'Đã hủy',
  CLOSED: 'Đã đóng',
  Closed: 'Đã đóng',
  CONFIRMED: 'Đã xác nhận',
  Confirmed: 'Đã xác nhận',
  DISCONTINUED: 'Ngừng dùng',
  Discontinued: 'Ngừng dùng',
  DRAFT: 'Nháp',
  Draft: 'Nháp',
  IN_DELIVERY: 'Đang giao',
  InDelivery: 'Đang giao',
  LOCKED: 'Đã khóa',
  Locked: 'Đã khóa',
  NEW: 'Mới',
  New: 'Mới',
  OPEN: 'Mở',
  Open: 'Mở',
  ORDERED: 'Đã đặt hàng',
  Ordered: 'Đã đặt hàng',
  PAID: 'Đã thanh toán',
  Paid: 'Đã thanh toán',
  PENDING: 'Chờ xử lý',
  Pending: 'Chờ xử lý',
  PENDING_APPROVAL: 'Chờ duyệt',
  PendingApproval: 'Chờ duyệt',
  RECORDED: 'Đã ghi nhận',
  Recorded: 'Đã ghi nhận',
  REJECTED: 'Từ chối',
  Rejected: 'Từ chối',
  RESIGNED: 'Đã nghỉ việc',
  Resigned: 'Đã nghỉ việc',
  STARTED: 'Đang sản xuất',
  Started: 'Đang sản xuất',
  UNPAID: 'Chưa thanh toán',
  Unpaid: 'Chưa thanh toán',
  Rework: 'Cần sửa lại',
  Scrap: 'Hỏng không sửa được',
  ACHIEVED: 'Đạt',
  NOT_ACHIEVED: 'Không đạt',
  All: 'Tất cả',
  Team: 'Theo tổ',
  Role: 'Theo vai trò',
  Urgent: 'Khẩn',
  Normal: 'Thường',
  PieceRate: 'Khoán sản phẩm',
  Daily: 'Theo ngày',
  Monthly: 'Theo tháng',
};

/** Giá trị đơn giản của log đọc được: ngày giờ theo định dạng chung, có / không, mã trạng thái → tiếng Việt, tiền theo VNĐ. */
export const formatAuditValue = (key: string, value: unknown): string => {
  if (value === null || value === undefined || value === '') return '—';
  if (typeof value === 'boolean') return value ? 'Có' : 'Không';
  if (typeof value === 'number') {
    return /Amount|Price|Rate|Total|Cost/.test(key) ? formatCurrency(value) : formatQty(value);
  }
  if (Array.isArray(value)) return `${value.length} mục`;
  if (typeof value === 'object')
    return Object.keys(value as object).length === 0 ? '—' : 'Có thay đổi';
  const text = String(value);
  if (/^\d{4}-\d{2}-\d{2}T/.test(text)) return formatDateTime(text);
  if (/^\d{4}-\d{2}-\d{2}$/.test(text)) return formatDate(text);
  return VALUE_LABEL[text] ?? text;
};

/** Câu mô tả của một dòng log: dùng câu BE đã dựng lúc ghi, dòng cũ chưa có thì tự ghép "{hành động} {đối tượng} #{Id}". */
export const auditSummary = (row: {
  summary?: string | null;
  action: string;
  tableName: string;
  recordId: number;
}): string =>
  row.summary ??
  `${auditActionText(row.action)} ${auditTableLabel(row.tableName).toLowerCase()} #${row.recordId}`;
