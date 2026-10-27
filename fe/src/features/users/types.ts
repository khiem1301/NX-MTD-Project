import type { PayType } from '@/constants/hr';
import type { Role } from '@/constants/roles';
import type { AuditEntry } from '@/types/attachment';

export type EmployeeListItem = {
  employeeId: number;
  employeeCode: string;
  fullName: string;
  role: Role;
  roleName: string;
  teamId: number | null;
  teamName: string | null;
  payType: PayType;
  currentGradeCode: string | null;
  attendanceCode: number | null;
  status: 'Active' | 'Resigned';
  /** Quản đốc không nhận trường này (null). */
  accountStatus: 'Active' | 'Locked' | null;
  startDate: string;
};

export type AccountInfo = {
  accountId: number;
  username: string;
  status: 'Active' | 'Locked';
  isTemporaryPassword: boolean;
  lockedAt: string | null;
  lockReason: string | null;
};

export type EmployeeDetail = {
  employeeId: number;
  employeeCode: string;
  fullName: string;
  gender: string | null;
  dateOfBirth: string | null;
  phone: string | null;
  avatarUrl: string | null;
  role: Role;
  roleName: string;
  teamId: number | null;
  teamName: string | null;
  payType: PayType;
  currentGradeCode: string | null;
  attendanceCode: number | null;
  startDate: string;
  resignDate: string | null;
  status: 'Active' | 'Resigned';
  rowVer: string;
  account: AccountInfo | null;
  history: AuditEntry[];
};

export type EmployeeQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  teamId?: number;
  role?: Role;
  status?: 'Active' | 'Resigned';
};

export type CreateEmployeePayload = {
  employeeCode: string;
  fullName: string;
  gender?: string | null;
  dateOfBirth?: string | null;
  phone?: string | null;
  role: Role;
  teamId?: number | null;
  payType: PayType;
  startDate: string;
  attendanceCode?: number | null;
};

export type AdminUpdatePayload = Omit<CreateEmployeePayload, 'employeeCode'> & { rowVer: string };
export type ManagerUpdatePayload = {
  fullName: string;
  gender?: string | null;
  dateOfBirth?: string | null;
  phone?: string | null;
  role: Role;
  teamId?: number | null;
  startDate: string;
  attendanceCode?: number | null;
  rowVer: string;
};

/** Trả MỘT lần: không lưu vào store / localStorage, không log. */
export type TempPassword = { username: string; temporaryPassword: string };
export type CreatedEmployee = TempPassword & { employeeId: number };
