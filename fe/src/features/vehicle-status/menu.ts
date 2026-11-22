import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const vehicleStatusMenu: MenuEntry[] = [
  {
    key: 'vehicle-status',
    label: 'Xe và trạng thái',
    path: ROUTES.vehicleStatus,
    roles: [ROLES.Admin],
  },
];
