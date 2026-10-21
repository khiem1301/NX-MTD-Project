import { createBrowserRouter, Navigate } from 'react-router-dom';
import { ProtectedRoute } from '@/components/guards/ProtectedRoute';
import { MainLayout } from '@/components/layout/MainLayout';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { ACCOUNTING_REPORT_ROUTES } from '@/features/accounting-reports/routes';
import { ATTENDANCE_ROUTES } from '@/features/attendance/routes';
import { BREAK_EVEN_ROUTES } from '@/features/break-even/routes';
import { COST_SUMMARY_ROUTES } from '@/features/cost-summary/routes';
import { HomePage } from '@/features/dashboard';
import { REQUEST_CREATOR_ROLES, REQUEST_HANDLER_ROLES } from '@/features/requests/menu';
import { KPI_ROUTES } from '@/features/kpi/routes';
import { MEAL_ROUTES } from '@/features/meal/routes';
import { MONTHLY_COST_ROUTES } from '@/features/monthly-cost/routes';
import { PAYROLL_ROUTES } from '@/features/payroll/routes';
import { PURCHASE_ROUTES } from '@/features/purchase/routes';
import { REVENUE_ROUTES } from '@/features/revenue/routes';

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
    path: ROUTES.purchaseOrders,
    roles: [Manager],
    page: async () => (await warehouse()).PurchaseOrderListPage,
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

          // --- Suất ăn (Minh) ---
          {
            element: <ProtectedRoute roles={[ROLES.TeamLeader, ROLES.TeamDeputy]} />,
            children: [
              {
                path: MEAL_ROUTES.teamMeal,
                lazy: async () => ({ Component: (await import('@/features/meal')).TeamMealPage }),
              },
            ],
          },
          {
            element: <ProtectedRoute roles={[ROLES.Accountant]} />,
            children: [
              {
                path: MEAL_ROUTES.summary,
                lazy: async () => ({ Component: (await import('@/features/meal')).MealSummaryPage }),
              },
            ],
          },

          // --- Bảng lương (Minh) ---
          {
            path: PAYROLL_ROUTES.myPayslip,
            lazy: async () => ({ Component: (await import('@/features/payroll')).MyPayslipPage }),
          },
          {
            element: <ProtectedRoute roles={[ROLES.Worker]} />,
            children: [
              {
                path: PAYROLL_ROUTES.myEstimatedPay,
                lazy: async () => ({
                  Component: (await import('@/features/payroll')).MyEstimatedPayPage,
                }),
              },
            ],
          },
          {
            element: <ProtectedRoute roles={[ROLES.Accountant]} />,
            children: [
              {
                path: PAYROLL_ROUTES.accountant,
                lazy: async () => ({
                  Component: (await import('@/features/payroll')).AccountantPayrollPage,
                }),
              },
            ],
          },
          {
            element: <ProtectedRoute roles={[ROLES.Admin]} />,
            children: [
              {
                path: PAYROLL_ROUTES.approval,
                lazy: async () => ({
                  Component: (await import('@/features/payroll')).PayrollApprovalPage,
                }),
              },
            ],
          },

          // --- Nhà cung cấp + đơn mua + thanh toán (Minh, MI-07) ---
          {
            element: <ProtectedRoute roles={[ROLES.Admin]} />,
            children: [
              {
                path: PURCHASE_ROUTES.suppliers,
                lazy: async () => ({ Component: (await import('@/features/purchase')).SuppliersPage }),
              },
            ],
          },
          {
            element: <ProtectedRoute roles={[ROLES.Manager]} />,
            children: [
              {
                path: PURCHASE_ROUTES.orders,
                lazy: async () => ({ Component: (await import('@/features/purchase')).PurchaseOrdersPage }),
              },
            ],
          },
          {
            element: <ProtectedRoute roles={[ROLES.Accountant]} />,
            children: [
              {
                path: PURCHASE_ROUTES.payments,
                lazy: async () => ({ Component: (await import('@/features/purchase')).PurchasePaymentsPage }),
              },
            ],
          },

          // --- Chi phí tháng (Minh) ---
          {
            element: <ProtectedRoute roles={[ROLES.Accountant]} />,
            children: [
              {
                path: MONTHLY_COST_ROUTES.accountant,
                lazy: async () => ({
                  Component: (await import('@/features/monthly-cost')).MonthlyCostPage,
                }),
              },
            ],
          },

          // --- KPI tổ may (Minh) ---
          {
            element: <ProtectedRoute roles={[ROLES.Manager]} />,
            children: [
              {
                path: KPI_ROUTES.targets,
                lazy: async () => ({ Component: (await import('@/features/kpi')).KpiTargetListPage }),
              },
            ],
          },

          // --- KPI tổ may: Admin chỉ xem (Minh, MI-17, UC21) ---
          {
            element: <ProtectedRoute roles={[ROLES.Admin]} />,
            children: [
              {
                path: KPI_ROUTES.adminTargets,
                lazy: async () => ({ Component: (await import('@/features/kpi')).AdminKpiTargetPage }),
              },
            ],
          },

          // --- Doanh thu (Minh, UC27, GBR-02: Kế toán + Admin) ---
          {
            element: <ProtectedRoute roles={[ROLES.Accountant]} />,
            children: [
              {
                path: REVENUE_ROUTES.accountant,
                lazy: async () => ({ Component: (await import('@/features/revenue')).AccountantRevenuePage }),
              },
            ],
          },
          {
            element: <ProtectedRoute roles={[ROLES.Admin]} />,
            children: [
              {
                path: REVENUE_ROUTES.admin,
                lazy: async () => ({ Component: (await import('@/features/revenue')).AdminRevenuePage }),
              },

              // --- Hòa vốn + giá trị thặng dư (Minh, UC11, chỉ Admin, GBR-02) ---
              {
                path: BREAK_EVEN_ROUTES.admin,
                lazy: async () => ({ Component: (await import('@/features/break-even')).BreakEvenPage }),
              },

              // --- Tổng hợp chi phí (Minh, MI-19, UC12, chỉ Admin, GBR-02) ---
              {
                path: COST_SUMMARY_ROUTES.admin,
                lazy: async () => ({ Component: (await import('@/features/cost-summary')).CostSummaryPage }),
              },
            ],
          },

          // --- Báo cáo kế toán (Minh, MI-19, UC36, chỉ Kế toán) ---
          {
            element: <ProtectedRoute roles={[ROLES.Accountant]} />,
            children: [
              {
                path: ACCOUNTING_REPORT_ROUTES.accountant,
                lazy: async () => ({
                  Component: (await import('@/features/accounting-reports')).AccountingReportsPage,
                }),
              },
            ],
          },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to={ROUTES.home} replace /> },
]);
