import { createBrowserRouter, Navigate } from 'react-router-dom';
import { ProtectedRoute } from '@/components/guards/ProtectedRoute';
import { MainLayout } from '@/components/layout/MainLayout';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { HomePage } from '@/features/dashboard';

const { Admin, Manager, WarehouseKeeper, TeamLeader, TeamDeputy } = ROLES;
const warehouse = () => import('@/features/warehouse');
const cutting = () => import('@/features/cutting');

// Kho, cắt (Thái): mỗi trang lazy load + chỉ mở cho role được dùng.
const warehouseRoutes = [
  {
    path: ROUTES.materials,
    roles: [WarehouseKeeper],
    page: async () => (await warehouse()).MaterialListPage,
  },
  {
    path: ROUTES.orderMaterials,
    roles: [Admin, Manager],
    page: async () => (await warehouse()).OrderMaterialPage,
  },
  {
    path: ROUTES.materialReceipts,
    roles: [WarehouseKeeper, Manager],
    page: async () => (await warehouse()).MaterialReceiptListPage,
  },
  {
    path: ROUTES.materialIssues,
    roles: [WarehouseKeeper, TeamLeader, TeamDeputy],
    page: async () => (await warehouse()).MaterialIssueListPage,
  },
  {
    path: ROUTES.pieceHoldings,
    roles: [TeamLeader, TeamDeputy],
    page: async () => (await warehouse()).PieceHoldingsPage,
  },
  {
    path: ROUTES.inventories,
    roles: [WarehouseKeeper, Manager, Admin],
    page: async () => (await warehouse()).InventoryPage,
  },
  {
    path: ROUTES.workOrders,
    roles: [Manager, WarehouseKeeper, TeamLeader, TeamDeputy],
    page: async () => (await cutting()).WorkOrderListPage,
  },
  {
    path: ROUTES.cuttingLays,
    roles: [TeamLeader, TeamDeputy],
    page: async () => (await cutting()).CuttingLayPage,
  },
  {
    path: ROUTES.cutHandovers,
    roles: [TeamLeader, TeamDeputy, WarehouseKeeper],
    page: async () => (await cutting()).CutHandoverPage,
  },
].map(({ path, roles, page }) => ({
  element: <ProtectedRoute roles={roles} />,
  children: [{ path, lazy: async () => ({ Component: await page() }) }],
}));

export const router = createBrowserRouter([
  {
    path: ROUTES.login,
    lazy: async () => ({ Component: (await import('@/features/auth')).LoginPage }),
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <MainLayout />,
        children: [
          { index: true, element: <HomePage /> },
          ...warehouseRoutes,
          // Route của từng luồng: thêm tại đây, bọc <ProtectedRoute roles={[...]} /> nếu giới hạn role.
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to={ROUTES.home} replace /> },
]);
