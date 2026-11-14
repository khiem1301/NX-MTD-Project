import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const materialReturnMenu: MenuEntry[] = [
  {
    key: 'material-returns',
    label: 'Trả hàng về kho',
    path: ROUTES.materialReturns,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy, ROLES.WarehouseKeeper],
  },
];
