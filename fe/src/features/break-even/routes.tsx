import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { BREAK_EVEN_ROUTES } from '@/constants/routes';

/** Hòa vốn + giá trị thặng dư (Minh, UC11, chỉ Admin, GBR-02). */
export const breakEvenRoutes = toPageRoutes([
  {
    path: BREAK_EVEN_ROUTES.admin,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/BreakEvenPage')).BreakEvenPage,
  },
]);
