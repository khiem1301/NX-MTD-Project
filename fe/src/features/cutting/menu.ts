import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

const { Manager, WarehouseKeeper, TeamLeader, TeamDeputy } = ROLES;

export const cuttingMenu: MenuEntry[] = [
  {
    key: 'work-orders',
    label: 'Lệnh cắt',
    path: ROUTES.workOrders,
    roles: [Manager, WarehouseKeeper, TeamLeader, TeamDeputy],
  },
  {
    key: 'cutting-lays',
    label: 'Bàn cắt',
    path: ROUTES.cuttingLays,
    roles: [TeamLeader, TeamDeputy],
  },
  {
    key: 'cut-handovers',
    label: 'Bàn giao BTP',
    path: ROUTES.cutHandovers,
    roles: [TeamLeader, TeamDeputy, WarehouseKeeper],
  },
];
