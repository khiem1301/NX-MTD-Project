import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { ATTENDANCE_ROUTES } from './routes';
import { ATTENDANCE_TEXT } from './constants';

export const attendanceMenu: MenuEntry[] = [
  {
    key: 'attendance',
    label: ATTENDANCE_TEXT.pageTitle,
    path: ATTENDANCE_ROUTES.attendance,
    roles: [ROLES.Accountant],
  },
  {
    key: 'team-attendance',
    label: ATTENDANCE_TEXT.teamPageTitle,
    path: ATTENDANCE_ROUTES.teamAttendance,
    roles: [ROLES.TeamLeader, ROLES.TeamDeputy],
  },
  {
    key: 'my-attendance',
    label: ATTENDANCE_TEXT.myPageTitle,
    path: ATTENDANCE_ROUTES.myAttendance,
    roles: 'all',
  },
];
