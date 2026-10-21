/** Trạng thái bảng lương tháng (Payroll.Status) — UC31/UC32. */
export type PayrollStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected';

/** Nhóm khoản trên phiếu lương (PayslipItem.ItemGroup). */
export type PayslipItemGroup = 'Addition' | 'Deduction' | 'Insurance' | 'Info';

/** Trạng thái một dòng trong bảng xem trước Excel (không lưu DB) — MSG24. */
export type PayrollRowStatus = 'Valid' | 'Error' | 'Duplicate';

/** Cách hiển thị giá trị một khoản: tiền, số (ngày/giờ công) hoặc chữ (bậc thợ). */
export type PayslipItemUnit = 'Money' | 'Number' | 'Text';

export type PayslipItem = {
  itemGroup: PayslipItemGroup;
  itemName: string;
  amount: number;
  sortOrder: number;
  /** Nhóm hiển thị theo mẫu phiếu lương (BE sắp sẵn theo thứ tự mẫu). */
  section: string;
  unit: PayslipItemUnit;
  textValue: string | null;
};

export type Payslip = {
  payslipId: number;
  employeeId: number;
  employeeCode: string;
  fullName: string;
  teamName: string | null;
  workDays: number | null;
  totalAdditions: number;
  totalDeductions: number;
  insurance: number;
  netPay: number;
  items: PayslipItem[];
};

export type PayrollSummary = {
  payrollId: number;
  periodId: number;
  periodMonth: string;
  status: PayrollStatus;
  /** Có file Excel gốc Kế toán đã nhập để tải về (BE không trả đường dẫn lưu trữ). */
  hasImportedFile: boolean;
  importedAt: string | null;
  totalAmount: number;
  payslipCount: number;
  submittedAt: string | null;
  approvedAt: string | null;
  rejectReason: string | null;
  reopenReason: string | null;
};

export type PayrollDetail = {
  payrollId: number;
  periodId: number;
  periodMonth: string;
  status: PayrollStatus;
  hasImportedFile: boolean;
  totalAmount: number;
  submittedAt: string | null;
  approvedAt: string | null;
  rejectReason: string | null;
  reopenReason: string | null;
  payslips: Payslip[];
};

/** Một nhân viên trong bảng xem trước; message là lý do lỗi/trùng hoặc cảnh báo khi dòng vẫn hợp lệ. */
export type PayrollExcelRowPreview = {
  sheetName: string;
  rowNumber: number;
  attendanceCode: string | null;
  employeeName: string | null;
  workDays: number | null;
  netPay: number | null;
  itemCount: number;
  rowStatus: PayrollRowStatus;
  message: string | null;
};

export type PayrollExcelPreview = {
  validCount: number;
  errorCount: number;
  duplicateCount: number;
  rows: PayrollExcelRowPreview[];
};

export type PayrollImportResult = {
  payrollId: number;
  periodMonth: string;
  validCount: number;
  errorCount: number;
  duplicateCount: number;
  payslipCount: number;
  totalAmount: number;
};

export type RejectPayrollPayload = { reason: string };
export type ReopenPayrollPayload = { reason: string };

export type MyPayslipSummary = {
  periodMonth: string;
  netPay: number;
  approvedAt: string;
};

export type MyPayslipDetail = {
  periodMonth: string;
  workDays: number | null;
  totalAdditions: number;
  totalDeductions: number;
  insurance: number;
  netPay: number;
  approvedAt: string;
  items: PayslipItem[];
};
