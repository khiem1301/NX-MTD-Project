import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROUTES } from '@/constants/routes';

/** Hồ sơ cá nhân: mọi vai trò (Khiêm). */
export const profileRoutes = toPageRoutes([
  { path: ROUTES.profile, page: async () => (await import('./pages/ProfilePage')).ProfilePage },
]);
