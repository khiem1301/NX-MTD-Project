import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const notificationMenu: MenuEntry[] = [
  { key: 'notifications', label: 'Thông báo', path: ROUTES.notifications, roles: 'all' },
];
