import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Bàn giao thành phẩm (Thái, TH-11). */
export const fgHandoverRoutes = toPageRoutes([
  {
    path: ROUTES.fgHandovers,
    roles: [ROLES.QaQc, ROLES.WarehouseKeeper],
    page: async () => (await import('./pages/FgHandoverPage')).FgHandoverPage,
  },
]);
