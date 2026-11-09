import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Tổ (Khiêm). */
export const teamRoutes = toPageRoutes([
  {
    path: ROUTES.teams,
    roles: [ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/TeamsPage')).TeamsPage,
  },
]);
