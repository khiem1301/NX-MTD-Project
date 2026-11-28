import { ROLES, type Role } from '@/constants/roles';
import {
  ATTENDANCE_ROUTES,
  MEAL_ROUTES,
  PAYROLL_ROUTES,
  PRODUCTION_ROUTES,
  ROUTES,
} from '@/constants/routes';
import type { NavLeaf } from './menuModel';

/** Vai trò làm việc tại xưởng, chủ yếu dùng điện thoại. */
export const WORKSHOP_ROLES: readonly Role[] = [
  ROLES.Worker,
  ROLES.TeamLeader,
  ROLES.TeamDeputy,
  ROLES.QaQc,
  ROLES.Electrician,
];

/** Khớp media query trong styles/workshop-mobile.css. */
export const MOBILE_QUERY = '(max-width: 767px)';
export const MOBILE_NAV_LABEL = { home: 'Trang chủ', menu: 'Menu', aria: 'Lối tắt' } as const;
const MAX_SHORTCUTS = 3;

export type MobileShortcut = { path: string; label: string };

const MY_ATTENDANCE = { path: ATTENDANCE_ROUTES.myAttendance, label: 'Ngày công' };
const MY_MEAL = { path: MEAL_ROUTES.myMeal, label: 'Suất ăn' };
const MY_REQUESTS = { path: ROUTES.requests, label: 'Đơn của tôi' };

const LEADER_SHORTCUTS: MobileShortcut[] = [
  { path: PRODUCTION_ROUTES.output, label: 'Sản lượng' },
  { path: ROUTES.pieceMovements, label: 'Giao hàng' },
  { path: ROUTES.workOrders, label: 'Lệnh cắt' },
  { path: ROUTES.cuttingLays, label: 'Bàn cắt' },
  { path: MEAL_ROUTES.teamMeal, label: 'Suất ăn' },
];

/** Theo thứ tự ưu tiên; mục người dùng không được vào (VD khác loại tổ) bị bỏ, lấy mục kế tiếp. */
const SHORTCUTS: Partial<Record<Role, MobileShortcut[]>> = {
  [ROLES.Worker]: [
    { path: ROUTES.myPieceMovements, label: 'Được giao' },
    { path: PAYROLL_ROUTES.myEstimatedPay, label: 'Lương tạm' },
    MY_ATTENDANCE,
    MY_MEAL,
    { path: PAYROLL_ROUTES.myPayslip, label: 'Phiếu lương' },
  ],
  [ROLES.TeamLeader]: LEADER_SHORTCUTS,
  [ROLES.TeamDeputy]: LEADER_SHORTCUTS,
  [ROLES.QaQc]: [
    { path: PRODUCTION_ROUTES.qcBatches, label: 'Kiểm hàng' },
    { path: ROUTES.fgHandovers, label: 'Bàn giao' },
    MY_ATTENDANCE,
  ],
  [ROLES.Electrician]: [
    { path: ROUTES.requestInbox, label: 'Cần xử lý' },
    MY_REQUESTS,
    MY_ATTENDANCE,
  ],
};

export const isWorkshopRole = (role: Role | undefined) =>
  role !== undefined && WORKSHOP_ROLES.includes(role);

export function mobileShortcuts(
  role: Role | undefined,
  leaves: readonly NavLeaf[],
): MobileShortcut[] {
  const allowed = new Set(leaves.map((leaf) => leaf.path));
  const candidates = role === undefined ? [] : (SHORTCUTS[role] ?? []);
  return candidates.filter((item) => allowed.has(item.path)).slice(0, MAX_SHORTCUTS);
}
