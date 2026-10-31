export type SyncSource = 'RjSync' | 'ExcelImport';
export type SyncStatus = 'Running' | 'Succeeded' | 'Failed';
export type TimesheetSource = 'FINGERPRINT' | 'ADJUSTMENT';

/** Mã DB như BE nhận / trả cho chấm bù (khác quy ước PascalCase của các enum khác). */
export type AdjustmentType =
  'FORGOT_PUNCH' | 'FINGERPRINT_FAIL' | 'OFFSITE_WORK' | 'APPROVED_REQUEST';

export type AttendanceAdjustment = {
  attendanceAdjustmentId: number;
  employeeId: number;
  employeeCode: string;
  fullName: string;
  workDate: string;
  adjustmentType: string;
  reason: string;
  requestId: number | null;
  checkIn: string | null;
  checkOut: string | null;
  recordedAt: string;
};

export type CreateAdjustmentPayload = {
  employeeId: number;
  workDate: string;
  adjustmentType: AdjustmentType;
  reason: string;
  requestId?: number;
  checkIn?: string;
  checkOut?: string;
};

export type AdjustmentFilter = {
  fromDate: string;
  toDate: string;
  keyword?: string;
  page: number;
  pageSize: number;
};

export type SyncSession = {
  syncSessionId: number;
  source: SyncSource;
  startedAt: string;
  finishedAt: string | null;
  lastSourceRecordId: number | null;
  recordsReceived: number;
  recordsInserted: number;
  status: SyncStatus;
  errorMessage: string | null;
  fileName: string | null;
};

export type SyncHealth = {
  lastSucceededAt: string | null;
  minutesSinceLastSuccess: number | null;
  alertThresholdMinutes: number;
  isOverdue: boolean;
  unmappedCodeCount: number;
};

export type UnmappedCode = {
  attendanceCode: number;
  punchCount: number;
  firstPunchAt: string;
  lastPunchAt: string;
};

export type MapAttendanceCodeRequest = {
  attendanceCode: number;
  employeeId: number;
};

export type DailyTimesheet = {
  dailyTimesheetId: number;
  employeeId: number;
  employeeCode: string;
  fullName: string;
  teamName: string | null;
  workDate: string;
  checkIn: string | null;
  checkOut: string | null;
  workedHours: number;
  workdayValue: number;
  overtimeHours: number;
  isMissingPunch: boolean;
  hasExtraPunch: boolean;
  isSunday: boolean;
  source: TimesheetSource;
  note: string | null;
  updatedAt: string;
};

export type TimesheetSummary = {
  employeeCount: number;
  totalWorkdays: number;
  totalOvertimeHours: number;
  missingPunchCount: number;
};

/** Một dòng quẹt đã đọc được từ file Excel dự phòng (UC29), chưa ghi DB. */
export type AttendanceExcelPreviewRow = {
  attendanceCode: number;
  workDate: string;
  checkIn: string | null;
  checkOut: string | null;
};

export type AttendanceExcelPreview = {
  rowCount: number;
  punchCount: number;
  rows: AttendanceExcelPreviewRow[];
};

/** Kết quả sau khi nhập file (cùng hình dạng với kết quả đồng bộ RJ). */
export type AttendanceImportResult = {
  syncSessionId: number;
  received: number;
  inserted: number;
  skippedLockedMonth: number;
  duplicates: number;
  unmapped: number;
};

/** Bộ lọc gửi lên API; fromDate / toDate dạng yyyy-MM-dd. */
export type TimesheetFilter = {
  fromDate: string;
  toDate: string;
  teamId?: number;
  employeeId?: number;
  onlyMissingPunch?: boolean;
  keyword?: string;
  page: number;
  pageSize: number;
};
