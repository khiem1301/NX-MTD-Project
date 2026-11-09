import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { REVENUE_ROUTES } from '@/constants/routes';

/** Doanh thu (Minh, UC27, GBR-02: Kế toán + Admin). */
export const revenueRoutes = toPageRoutes([
  {
    path: REVENUE_ROUTES.accountant,
    roles: [ROLES.Accountant],
    page: async () => (await import('./pages/AccountantRevenuePage')).AccountantRevenuePage,
  },
  {
    path: REVENUE_ROUTES.admin,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/AdminRevenuePage')).AdminRevenuePage,
  },
]);
