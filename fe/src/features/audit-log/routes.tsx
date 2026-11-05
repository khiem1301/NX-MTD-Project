import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Nhật ký thay đổi (Khiêm, KH-22). */
export const auditLogRoutes = toPageRoutes([
  {
    path: ROUTES.auditLogs,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/AuditLogPage')).AuditLogPage,
  },
]);
