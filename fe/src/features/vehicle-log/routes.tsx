import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Sổ xe (Thảo, TA-03). */
export const vehicleLogRoutes = toPageRoutes([
  {
    path: ROUTES.vehicleLog,
    roles: [ROLES.Accountant],
    page: async () => (await import('./pages/VehicleLogPage')).VehicleLogPage,
  },
]);
