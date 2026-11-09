import { createBrowserRouter, Navigate } from 'react-router-dom';
import { ProtectedRoute } from '@/components/guards/ProtectedRoute';
import { MainLayout } from '@/components/layout/MainLayout';
import { ROUTES } from '@/constants/routes';
import { HomePage } from '@/features/dashboard';
import { accountingReportRoutes } from '@/features/accounting-reports/routes';
import { attendanceRoutes } from '@/features/attendance/routes';
import { auditLogRoutes } from '@/features/audit-log/routes';
import { breakEvenRoutes } from '@/features/break-even/routes';
import { costSummaryRoutes } from '@/features/cost-summary/routes';
import { cuttingRoutes } from '@/features/cutting/routes';
import { dispatchRoutes } from '@/features/dispatch/routes';
import { fgHandoverRoutes } from '@/features/fg-handover/routes';
import { gradeRoutes } from '@/features/grade/routes';
import { kpiRoutes } from '@/features/kpi/routes';
import { materialReturnRoutes } from '@/features/material-return/routes';
import { mealRoutes } from '@/features/meal/routes';
import { monthlyCostRoutes } from '@/features/monthly-cost/routes';
import { notificationRoutes } from '@/features/notification/routes';
import { orderRoutes } from '@/features/orders/routes';
import { payrollRoutes } from '@/features/payroll/routes';
import { pieceMovementRoutes } from '@/features/piece-movement/routes';
import { productionRoutes } from '@/features/production/routes';
import { profileRoutes } from '@/features/profile/routes';
import { purchaseRoutes } from '@/features/purchase/routes';
import { requestRoutes } from '@/features/requests/routes';
import { revenueRoutes } from '@/features/revenue/routes';
import { stocktakeRoutes } from '@/features/stocktake/routes';
import { teamRoutes } from '@/features/teams/routes';
import { userRoutes } from '@/features/users/routes';
import { vehicleLogRoutes } from '@/features/vehicle-log/routes';
import { warehouseRoutes } from '@/features/warehouse/routes';

// Mỗi luồng khai báo route ở features/<x>/routes.tsx (lazy load theo trang) rồi thêm một dòng vào mảng dưới.
// Chỉ import routes.tsx / menu.ts của feature: import index.ts sẽ kéo cả feature vào bundle chính.
const featureRoutes = [
  ...profileRoutes, // Khiêm
  ...userRoutes, // Khiêm
  ...teamRoutes, // Khiêm
  ...orderRoutes, // Khiêm
  ...requestRoutes, // Khiêm
  ...notificationRoutes, // Khiêm (KH-12)
  ...gradeRoutes, // Khiêm (KH-14)
  ...auditLogRoutes, // Khiêm (KH-22)
  ...warehouseRoutes, // Thái
  ...cuttingRoutes, // Thái
  ...fgHandoverRoutes, // Thái (TH-11)
  ...dispatchRoutes, // Thái (TH-14)
  ...materialReturnRoutes, // Thái (TH-10)
  ...pieceMovementRoutes, // Thái (TH-19)
  ...stocktakeRoutes, // Thảo (TA-02)
  ...vehicleLogRoutes, // Thảo (TA-03)
  ...productionRoutes, // Minh làm hộ Kiều (KI-01..KI-11)
  ...attendanceRoutes, // Minh
  ...mealRoutes, // Minh
  ...payrollRoutes, // Minh
  ...purchaseRoutes, // Minh (MI-07)
  ...monthlyCostRoutes, // Minh
  ...kpiRoutes, // Minh
  ...revenueRoutes, // Minh
  ...breakEvenRoutes, // Minh
  ...costSummaryRoutes, // Minh (MI-19, UC12)
  ...accountingReportRoutes, // Minh (MI-19, UC36)
];

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
        children: [{ index: true, element: <HomePage /> }, ...featureRoutes],
      },
    ],
  },
  { path: '*', element: <Navigate to={ROUTES.home} replace /> },
]);
