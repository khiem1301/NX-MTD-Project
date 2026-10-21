import type { StatusLabel } from '@/components/common/StatusTag';
import type { Role } from '@/constants/roles';

export const ROLE_LABEL: Record<Role, string> = {
  Admin: 'Admin',
  Manager: 'Quản đốc',
  Accountant: 'Kế toán',
  WarehouseKeeper: 'Thủ kho',
  TeamLeader: 'Tổ trưởng',
  TeamDeputy: 'Tổ phó',
  QaQc: 'QA/QC',
  Electrician: 'Cơ điện',
  Worker: 'Công nhân',
};

/** UC16: Quản đốc không thấy nhân viên có các vai trò này (khớp RoleCodes.HiddenFromManager ở BE). */
export const MANAGER_HIDDEN_ROLES: readonly Role[] = ['Admin', 'Manager', 'Accountant'];

export type PayType = 'PieceRate' | 'Daily' | 'Monthly';
export const PAY_TYPE_LABEL: Record<PayType, string> = {
  PieceRate: 'Khoán sản phẩm',
  Daily: 'Theo ngày công',
  Monthly: 'Lương tháng',
};

export type TeamType = 'Cutting' | 'Sewing' | 'Office';
export const TEAM_TYPE_LABEL: Record<TeamType, string> = { Cutting: 'Tổ cắt', Sewing: 'Tổ may', Office: 'Văn phòng' };

export const EMPLOYEE_STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: 'Đang làm', color: 'green' },
  Resigned: { text: 'Đã nghỉ', color: 'default' },
};

export const ACCOUNT_STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: 'Hoạt động', color: 'green' },
  Locked: { text: 'Đã khóa', color: 'red' },
};

export const TEAM_STATUS_TAG: Record<string, StatusLabel> = {
  Active: { text: 'Hoạt động', color: 'green' },
  Discontinued: { text: 'Ngừng', color: 'default' },
};

/** Gợi ý kiểu lương khi chọn vai trò + loại tổ (chỉ gợi ý, người dùng sửa được). */
export const suggestPayType = (role: Role | undefined, teamType: TeamType | undefined): PayType | undefined => {
  if (!role) return undefined;
  if (role === 'Worker' && teamType === 'Sewing') return 'PieceRate';
  if ((role === 'Worker' && teamType === 'Cutting') || role === 'QaQc') return 'Daily';
  return 'Monthly';
};
