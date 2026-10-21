import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { BREAK_EVEN_ROUTES } from './routes';
import { BREAK_EVEN_TEXT } from './constants';

/** UC11 — Hòa vốn + giá trị thặng dư: CHỈ Admin (dữ liệu "mật", GBR-02). */
export const breakEvenMenu: MenuEntry[] = [
  {
    key: 'break-even',
    label: BREAK_EVEN_TEXT.pageTitle,
    path: BREAK_EVEN_ROUTES.admin,
    roles: [ROLES.Admin],
  },
];
