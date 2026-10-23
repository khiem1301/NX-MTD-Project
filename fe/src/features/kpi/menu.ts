import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { KPI_ROUTES } from './routes';
import { BTP_TEXT, KPI_TEXT } from './constants';

export const kpiMenu: MenuEntry[] = [
  {
    key: 'kpi-targets',
    label: KPI_TEXT.listPageTitle,
    path: KPI_ROUTES.targets,
    roles: [ROLES.Manager],
  },
  {
    key: 'kpi-btp-compare',
    label: BTP_TEXT.menu,
    path: KPI_ROUTES.btpCompare,
    roles: [ROLES.Manager, ROLES.Admin, ROLES.WarehouseKeeper, ROLES.TeamLeader, ROLES.TeamDeputy],
  },
  {
    key: 'admin-kpi-targets',
    label: KPI_TEXT.adminPageTitle,
    path: KPI_ROUTES.adminTargets,
    roles: [ROLES.Admin],
  },
];
