import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ATTENDANCE_ROUTES } from '@/constants/routes';

/** Chấm công (Minh). */
export const attendanceRoutes = toPageRoutes([
  {
    path: ATTENDANCE_ROUTES.myAttendance,
    roles: Object.values(ROLES).filter((r) => r !== ROLES.Admin),
    page: async () => (await import('./pages/MyAttendancePage')).MyAttendancePage,
  },
  {
    path: ATTENDANCE_ROUTES.attendance,
    roles: [ROLES.Admin, ROLES.Manager, ROLES.Accountant],
    page: async () => (await import('./pages/AttendancePage')).AttendancePage,
  },
  {
    path: ATTENDANCE_ROUTES.teamAttendance,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
    page: async () => (await import('./pages/TeamAttendancePage')).TeamAttendancePage,
  },
]);
