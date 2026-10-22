import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const stocktakeMenu: MenuEntry[] = [
  {
    key: 'stocktakes',
    label: 'Kiểm kê kho',
    path: ROUTES.stocktakes,
    roles: [ROLES.WarehouseKeeper, ROLES.Manager, ROLES.Admin],
  },
];
