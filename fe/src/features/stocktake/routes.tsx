import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Kiểm kê (Thảo, TA-02). */
export const stocktakeRoutes = toPageRoutes([
  {
    path: ROUTES.stocktakes,
    roles: [ROLES.WarehouseKeeper, ROLES.Manager, ROLES.Admin],
    page: async () => (await import('./pages/StocktakePage')).StocktakePage,
  },
]);
