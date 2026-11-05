import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Phiếu xuất hàng (Thái, TH-14). */
export const dispatchRoutes = toPageRoutes([
  {
    path: ROUTES.dispatchNotes,
    roles: [ROLES.Admin, ROLES.Manager, ROLES.WarehouseKeeper, ROLES.Accountant],
    page: async () => (await import('./pages/DispatchNotesPage')).DispatchNotesPage,
  },
]);
