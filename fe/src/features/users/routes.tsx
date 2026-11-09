import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Nhân viên: Admin và Quản đốc dùng API theo vai trò (Khiêm). */
export const userRoutes = toPageRoutes([
  {
    path: ROUTES.adminEmployees,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/EmployeesPage')).EmployeesPage,
  },
  {
    path: `${ROUTES.adminEmployees}/:id`,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/EmployeeDetailRedirect')).EmployeeDetailRedirect,
  },
  {
    path: ROUTES.managerEmployees,
    roles: [ROLES.Manager],
    page: async () => (await import('./pages/EmployeesPage')).EmployeesPage,
  },
  {
    path: `${ROUTES.managerEmployees}/:id`,
    roles: [ROLES.Manager],
    page: async () => (await import('./pages/EmployeeDetailRedirect')).EmployeeDetailRedirect,
  },
]);
