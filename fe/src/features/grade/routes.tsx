import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Bậc thợ (Khiêm, KH-14). */
export const gradeRoutes = toPageRoutes([
  {
    path: ROUTES.grades,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy, ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/GradePage')).GradePage,
  },
]);
