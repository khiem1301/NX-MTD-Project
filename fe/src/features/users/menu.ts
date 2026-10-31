import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const userMenu: MenuEntry[] = [
  { key: 'admin-employees', label: 'Nhân viên', path: ROUTES.adminEmployees, roles: [ROLES.Admin] },
  {
    key: 'manager-employees',
    label: 'Nhân viên',
    path: ROUTES.managerEmployees,
    roles: [ROLES.Manager],
  },
];
