import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const gradeMenu: MenuEntry[] = [
  {
    key: 'grades',
    label: 'Bậc thợ',
    path: ROUTES.grades,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy, ROLES.Admin, ROLES.Manager],
  },
];
