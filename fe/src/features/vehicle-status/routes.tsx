import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** DEV-ALL-159: Admin xem xe và trạng thái đang chạy. */
export const vehicleStatusRoutes = toPageRoutes([
  {
    path: ROUTES.vehicleStatus,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/VehicleStatusPage')).VehicleStatusPage,
  },
]);
