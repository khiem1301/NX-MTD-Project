import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const dispatchMenu: MenuEntry[] = [
  {
    key: 'dispatch-notes',
    label: 'Phiếu xuất hàng',
    path: ROUTES.dispatchNotes,
    roles: [ROLES.Manager, ROLES.WarehouseKeeper, ROLES.Accountant],
  },
];
