import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

const { Admin, Manager, Accountant, WarehouseKeeper, TeamLeader, TeamDeputy, Electrician } = ROLES;

/** Roles khớp RequestTypePolicies ở BE: ai tạo đơn / ai xử lý đơn. */
export const REQUEST_CREATOR_ROLES = [
  Electrician,
  TeamLeader,
  TeamDeputy,
  WarehouseKeeper,
] as const;
export const REQUEST_HANDLER_ROLES = [Admin, Manager, Accountant, WarehouseKeeper] as const;

export const requestMenu: MenuEntry[] = [
  { key: 'requests', label: 'Đơn từ của tôi', path: ROUTES.requests, roles: REQUEST_CREATOR_ROLES },
  {
    key: 'request-inbox',
    label: 'Đơn cần xử lý',
    path: ROUTES.requestInbox,
    roles: REQUEST_HANDLER_ROLES,
  },
];
