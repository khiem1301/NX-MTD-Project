import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Trả vải thừa (Thái, TH-10). */
export const materialReturnRoutes = toPageRoutes([
  {
    path: ROUTES.materialReturns,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy, ROLES.WarehouseKeeper],
    page: async () => (await import('./pages/MaterialReturnPage')).MaterialReturnPage,
  },
]);
