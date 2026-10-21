import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

export const profileMenu: MenuEntry[] = [
  { key: 'profile', label: 'Hồ sơ của tôi', path: ROUTES.profile, roles: 'all' },
];
