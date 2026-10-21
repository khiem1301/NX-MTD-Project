import { createBrowserRouter, Navigate } from 'react-router-dom';
import { ProtectedRoute } from '@/components/guards/ProtectedRoute';
import { MainLayout } from '@/components/layout/MainLayout';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { ATTENDANCE_ROUTES } from '@/features/attendance/routes';
import { HomePage } from '@/features/dashboard';
import { REQUEST_CREATOR_ROLES, REQUEST_HANDLER_ROLES } from '@/features/requests/menu';

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

// Đơn từ (Khiêm): quyền chi tiết theo (vai trò, loại đơn) do BE kiểm.
const requests = () => import('@/features/requests');
const requestRoutes = [
  {
    path: ROUTES.requests,
    roles: [...REQUEST_CREATOR_ROLES],
    page: async () => (await requests()).MyRequestsPage,
  },
  {
    path: ROUTES.requestInbox,
    roles: [...REQUEST_HANDLER_ROLES],
    page: async () => (await requests()).RequestInboxPage,
  },
].map(({ path, roles, page }) => ({
  element: <ProtectedRoute roles={roles} />,
  children: [{ path, lazy: async () => ({ Component: await page() }) }],
}));

// Mã hàng, đơn hàng (Khiêm): chỉ Admin vì có đơn giá CMT (D26).
const orders = () => import('@/features/orders');
const orderRoutes = [
  { path: ROUTES.orders, page: async () => (await orders()).OrdersPage },
  { path: ROUTES.orderNew, page: async () => (await orders()).OrderFormPage },
  { path: `${ROUTES.orders}/:id`, page: async () => (await orders()).OrderDetailPage },
  { path: `${ROUTES.orders}/:id/edit`, page: async () => (await orders()).OrderFormPage },
  { path: ROUTES.products, page: async () => (await orders()).ProductsPage },
  { path: ROUTES.customers, page: async () => (await orders()).CustomersPage },
].map(({ path, page }) => ({
  element: <ProtectedRoute roles={[Admin]} />,
  children: [{ path, lazy: async () => ({ Component: await page() }) }],
}));

// Nhân sự (Khiêm): hồ sơ mọi vai trò; nhân viên Admin + Quản đốc (API theo vai trò); tổ Admin + Quản đốc.
const profile = () => import('@/features/profile');
const users = () => import('@/features/users');
const teams = () => import('@/features/teams');
const hrRoutes = [
  { path: ROUTES.profile, roles: undefined, page: async () => (await profile()).ProfilePage },
  { path: ROUTES.adminEmployees, roles: [Admin], page: async () => (await users()).EmployeesPage },
  { path: `${ROUTES.adminEmployees}/:id`, roles: [Admin], page: async () => (await users()).EmployeeDetailPage },
  { path: ROUTES.managerEmployees, roles: [Manager], page: async () => (await users()).EmployeesPage },
  { path: `${ROUTES.managerEmployees}/:id`, roles: [Manager], page: async () => (await users()).EmployeeDetailPage },
  { path: ROUTES.teams, roles: [Admin, Manager], page: async () => (await teams()).TeamsPage },
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
          ...requestRoutes,
          ...orderRoutes,
          ...hrRoutes,
          // Route của từng luồng: thêm tại đây, bọc <ProtectedRoute roles={[...]} /> nếu giới hạn role.

          // --- Chấm công (Minh) ---
          {
            path: ATTENDANCE_ROUTES.myAttendance,
            lazy: async () => ({ Component: (await import('@/features/attendance')).MyAttendancePage }),
          },
          {
            element: <ProtectedRoute roles={[ROLES.Accountant]} />,
            children: [
              {
                path: ATTENDANCE_ROUTES.attendance,
                lazy: async () => ({ Component: (await import('@/features/attendance')).AttendancePage }),
              },
            ],
          },
          {
            element: <ProtectedRoute roles={[ROLES.TeamLeader, ROLES.TeamDeputy]} />,
            children: [
              {
                path: ATTENDANCE_ROUTES.teamAttendance,
                lazy: async () => ({ Component: (await import('@/features/attendance')).TeamAttendancePage }),
              },
            ],
          },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to={ROUTES.home} replace /> },
]);
