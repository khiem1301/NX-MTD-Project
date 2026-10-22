import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const vehicleLogMenu: MenuEntry[] = [
  {
    key: 'vehicle-log',
    label: 'Sổ xe và nhiên liệu',
    path: ROUTES.vehicleLog,
    roles: [ROLES.Accountant],
  },
];
