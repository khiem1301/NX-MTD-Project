import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { KPI_ROUTES } from '@/constants/routes';

/** KPI tổ may (Minh). */
export const kpiRoutes = toPageRoutes([
  {
    path: KPI_ROUTES.targets,
    roles: [ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/KpiTargetListPage')).KpiTargetListPage,
  },
]);
