import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { KPI_ROUTES } from './routes';
import { KPI_TEXT } from './constants';

export const kpiMenu: MenuEntry[] = [
  {
    key: 'kpi-targets',
    label: KPI_TEXT.listPageTitle,
    path: KPI_ROUTES.targets,
    roles: [ROLES.Manager],
  },
];
