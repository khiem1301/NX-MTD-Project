import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Thông báo (Khiêm, KH-12). */
export const notificationRoutes = toPageRoutes([
  {
    path: ROUTES.notifications,
    roles: Object.values(ROLES),
    page: async () => (await import('./pages/NotificationsPage')).NotificationsPage,
  },
]);
