import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Lệnh cắt, bàn cắt, bàn giao BTP (Thái). */
export const cuttingRoutes = toPageRoutes([
  {
    path: ROUTES.workOrders,
    roles: [ROLES.Admin, ROLES.Manager, ROLES.WarehouseKeeper, ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/WorkOrderListPage')).WorkOrderListPage,
  },
  {
    path: ROUTES.cuttingLays,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/CuttingLayPage')).CuttingLayPage,
  },
  {
    path: ROUTES.cutHandovers,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy, ROLES.WarehouseKeeper],
    page: async () => (await import('./pages/CutHandoverPage')).CutHandoverPage,
  },
]);
