export type SyncSource = 'RjSync' | 'ExcelImport';
export type SyncStatus = 'Running' | 'Succeeded' | 'Failed';
export type TimesheetSource = 'FINGERPRINT' | 'ADJUSTMENT';

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
