import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const teamMenu: MenuEntry[] = [
  { key: 'teams', label: 'Tổ', path: ROUTES.teams, roles: [ROLES.Admin, ROLES.Manager] },
];
