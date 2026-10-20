import { createBrowserRouter, Navigate } from 'react-router-dom';
import { ProtectedRoute } from '@/components/guards/ProtectedRoute';
import { MainLayout } from '@/components/layout/MainLayout';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import { ATTENDANCE_ROUTES } from '@/features/attendance/routes';
import { HomePage } from '@/features/dashboard';

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
