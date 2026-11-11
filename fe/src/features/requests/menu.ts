import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

const { Admin, Manager, Accountant, WarehouseKeeper, TeamLeader, TeamDeputy } = ROLES;

/** Roles khớp RequestTypePolicies ở BE: mọi người dùng tạo được đơn Khác (DEV-ALL-91). */
export const REQUEST_CREATOR_ROLES = Object.values(ROLES);
/** Tổ trưởng / tổ phó xử lý đơn Khác gửi tổ trưởng của tổ mình. */
export const REQUEST_HANDLER_ROLES = [
  Admin,
  Manager,
  Accountant,
  WarehouseKeeper,
  TeamLeader,
  TeamDeputy,
] as const;

export const requestMenu: MenuEntry[] = [
  { key: 'requests', label: 'Đơn từ của tôi', path: ROUTES.requests, roles: REQUEST_CREATOR_ROLES },
  {
    key: 'request-inbox',
    label: 'Đơn cần xử lý',
    path: ROUTES.requestInbox,
    roles: REQUEST_HANDLER_ROLES,
  },
];
