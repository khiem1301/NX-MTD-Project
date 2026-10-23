import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const auditLogMenu: MenuEntry[] = [
  { key: 'audit-logs', label: 'Nhật ký thay đổi', path: ROUTES.auditLogs, roles: [ROLES.Admin] },
];
