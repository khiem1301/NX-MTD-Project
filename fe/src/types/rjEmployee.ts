/** Nhân viên trên máy chấm công Ronald Jack Pro (snapshot do agent đồng bộ). Dùng cho chấm công và nhân viên. */
export type RjEmployee = {
  attendanceCode: number;
  fullName: string;
  rjEmployeeCode: string | null;
  isMapped: boolean;
  employeeId: number | null;
  employeeCode: string | null;
  employeeFullName: string | null;
  syncedAt: string;
};

export type ImportRjEmployeePayload = {
  attendanceCode: number;
  teamId?: number | null;
  payType?: string | null;
  startDate?: string | null;
};
