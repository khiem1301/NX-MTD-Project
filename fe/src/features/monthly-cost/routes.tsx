import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { MONTHLY_COST_ROUTES } from '@/constants/routes';

/** Chi phí tháng (Minh). */
export const monthlyCostRoutes = toPageRoutes([
  {
    path: MONTHLY_COST_ROUTES.accountant,
    roles: [ROLES.Accountant],
    page: async () => (await import('./pages/MonthlyCostPage')).MonthlyCostPage,
  },
]);
