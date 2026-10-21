import type { StatusLabel } from '@/components/common/StatusTag';
import type { PayrollRowStatus, PayrollStatus } from './types';

/** Chuỗi hiển thị của luồng bảng lương (MI-09). Thông báo kết quả thao tác nằm ở constants/messages.ts. */
export const PAYROLL_TEXT = {
  accountantPageTitle: 'Bảng lương',
  accountantPageSubtitle:
    'Xuất dữ liệu tính lương, nhập bảng lương đã tính trên Excel và gửi duyệt (hệ thống không tính lương).',
  adminPageTitle: 'Duyệt bảng lương',
  adminPageSubtitle: 'Xem bảng lương chờ duyệt, duyệt để khóa tháng, từ chối hoặc mở lại khi sai.',
  myPayslipPageTitle: 'Phiếu lương của tôi',
  myPayslipPageSubtitle: 'Chỉ xem được phiếu lương của tháng đã được Admin duyệt.',

  periodMonth: 'Tháng',
  export: 'Xuất dữ liệu tính lương',
  chooseFile: 'Chọn file Excel',
  preview: 'Xem trước',
  confirmImportTitle: 'Nhập bảng lương?',
  confirmImportContent:
    'Bảng lương cũ (nếu có) của tháng này sẽ bị thay bằng các nhân viên hợp lệ trong file. File gốc được lưu lại để giám đốc xem.',
  downloadImportedFile: 'Tải file gốc',
  chooseFileHint: 'File "Bảng thanh toán lương" gồm một sheet cho mỗi tổ (cột SLĐ là mã chấm công).',
  import: 'Nhập bảng lương',
  submit: 'Gửi duyệt',
  noPayrollYet: 'Tháng này chưa có bảng lương. Hãy nhập file Excel đã tính lương.',

  previewSummary: {
    valid: 'Hợp lệ',
    error: 'Lỗi',
    duplicate: 'Trùng',
  },

  previewColumns: {
    sheetName: 'Sheet (tổ)',
    rowNumber: 'Dòng',
    attendanceCode: 'SLĐ',
    employeeName: 'Họ tên',
    workDays: 'Số công',
    netPay: 'Thực lĩnh',
    itemCount: 'Số khoản',
    itemName: 'Tên khoản',
    amount: 'Số tiền',
    status: 'Trạng thái',
    message: 'Ghi chú',
  },

  rowStatusLabel: {
    Valid: 'Hợp lệ',
    Error: 'Lỗi',
    Duplicate: 'Trùng',
  },

  itemGroupLabel: {
    Addition: 'Khoản cộng',
    Deduction: 'Khoản trừ',
    Insurance: 'Bảo hiểm',
    Info: 'Thông tin',
  },

  statusLabel: {
    Draft: 'Nháp',
    PendingApproval: 'Chờ duyệt',
    Approved: 'Đã duyệt',
    Rejected: 'Từ chối',
  },

  summary: {
    periodMonth: 'Tháng',
    status: 'Trạng thái',
    totalAmount: 'Tổng quỹ lương',
    payslipCount: 'Số phiếu lương',
    submittedAt: 'Gửi duyệt lúc',
    approvedAt: 'Duyệt lúc',
    rejectReason: 'Lý do từ chối',
    reopenReason: 'Lý do mở lại gần nhất',
  },

  table: {
    employee: 'Nhân viên',
    team: 'Tổ',
    workDays: 'Số công',
    totalAdditions: 'Khoản cộng',
    totalDeductions: 'Khoản trừ',
    insurance: 'Bảo hiểm',
    netPay: 'Thực nhận',
    viewDetail: 'Xem chi tiết',
  },

  actions: {
    approve: 'Duyệt',
    reject: 'Từ chối',
    reopen: 'Mở lại về Nháp',
  },

  filter: {
    statusPlaceholder: 'Lọc theo trạng thái',
  },

  importDisabledHint:
    'Bảng lương tháng này đang chờ duyệt hoặc đã duyệt, không nhập lại được. Cần Admin từ chối hoặc mở lại trước.',

  myPayslip: {
    periodMonth: 'Tháng',
    netPay: 'Thực nhận',
    approvedAt: 'Duyệt lúc',
    back: 'Quay lại',
    additions: 'Khoản cộng',
    deductions: 'Khoản trừ',
    insurance: 'Bảo hiểm',
    info: 'Thông tin khác',
    netPayTotal: 'Thực nhận',
  },
} as const;

/** Giá trị khớp JSON BE trả (CodeText.ToPascal áp lên Payroll.Status khi serialize). */
export const PAYROLL_STATUS: Record<string, PayrollStatus> = {
  Draft: 'Draft',
  PendingApproval: 'PendingApproval',
  Approved: 'Approved',
  Rejected: 'Rejected',
};

export const PAYROLL_STATUS_LABEL: Record<PayrollStatus, StatusLabel> = {
  Draft: { text: PAYROLL_TEXT.statusLabel.Draft, color: 'default' },
  PendingApproval: { text: PAYROLL_TEXT.statusLabel.PendingApproval, color: 'gold' },
  Approved: { text: PAYROLL_TEXT.statusLabel.Approved, color: 'green' },
  Rejected: { text: PAYROLL_TEXT.statusLabel.Rejected, color: 'red' },
};

export const PAYROLL_ROW_STATUS_LABEL: Record<PayrollRowStatus, StatusLabel> = {
  Valid: { text: PAYROLL_TEXT.rowStatusLabel.Valid, color: 'green' },
  Error: { text: PAYROLL_TEXT.rowStatusLabel.Error, color: 'red' },
  Duplicate: { text: PAYROLL_TEXT.rowStatusLabel.Duplicate, color: 'gold' },
};

/** Lọc danh sách bảng lương cho Admin — bỏ Draft (kế toán chưa gửi thì Admin chưa cần thấy). */
export const PAYROLL_STATUS_FILTER_OPTIONS: { value: PayrollStatus; label: string }[] = [
  { value: 'PendingApproval', label: PAYROLL_STATUS_LABEL.PendingApproval.text },
  { value: 'Approved', label: PAYROLL_STATUS_LABEL.Approved.text },
  { value: 'Rejected', label: PAYROLL_STATUS_LABEL.Rejected.text },
  { value: 'Draft', label: PAYROLL_STATUS_LABEL.Draft.text },
];
