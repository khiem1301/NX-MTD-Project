import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const fgHandoverMenu: MenuEntry[] = [
  {
    key: 'fg-handovers',
    label: 'Bàn giao thành phẩm',
    path: ROUTES.fgHandovers,
    roles: [ROLES.QaQc, ROLES.WarehouseKeeper],
  },
];
