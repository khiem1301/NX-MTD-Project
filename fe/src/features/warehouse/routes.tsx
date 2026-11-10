import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Kho NVL, cấp phát, tồn kho (Thái). */
export const warehouseRoutes = toPageRoutes([
  {
    path: ROUTES.materials,
    roles: [ROLES.WarehouseKeeper, ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/MaterialListPage')).MaterialListPage,
  },
  {
    path: ROUTES.orderMaterials,
    roles: [ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/OrderMaterialLinkRedirect')).OrderMaterialLinkRedirect,
  },
  {
    path: ROUTES.incomingMaterials,
    roles: [ROLES.WarehouseKeeper],
    page: async () => (await import('./pages/IncomingMaterialsPage')).IncomingMaterialsPage,
  },
  {
    path: ROUTES.materialReceipts,
    roles: [ROLES.WarehouseKeeper, ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/MaterialReceiptListPage')).MaterialReceiptListPage,
  },
  {
    path: ROUTES.shortDeliveries,
    roles: [ROLES.WarehouseKeeper, ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/ShortDeliveryListPage')).ShortDeliveryListPage,
  },
  {
    path: ROUTES.materialIssues,
    roles: [ROLES.WarehouseKeeper, ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/MaterialIssueListPage')).MaterialIssueListPage,
  },
  {
    path: ROUTES.pieceHoldings,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/PieceHoldingsPage')).PieceHoldingsPage,
  },
  {
    path: ROUTES.inventories,
    roles: [ROLES.WarehouseKeeper, ROLES.Manager, ROLES.Admin],
    page: async () => (await import('./pages/InventoryPage')).InventoryPage,
  },
  {
    path: ROUTES.nxtReport,
    roles: [ROLES.WarehouseKeeper, ROLES.Manager, ROLES.Admin],
    page: async () => (await import('./pages/NxtReportPage')).NxtReportPage,
  },
]);
