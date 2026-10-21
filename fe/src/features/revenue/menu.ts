import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { REVENUE_ROUTES } from './routes';
import { REVENUE_TEXT } from './constants';

/** UC27 — Doanh thu: Kế toán + Admin đều có mục menu riêng (route khác nhau, GBR-02). */
export const revenueMenu: MenuEntry[] = [
  {
    key: 'revenue-accountant',
    label: REVENUE_TEXT.pageTitle,
    path: REVENUE_ROUTES.accountant,
    roles: [ROLES.Accountant],
  },
  {
    key: 'revenue-admin',
    label: REVENUE_TEXT.pageTitle,
    path: REVENUE_ROUTES.admin,
    roles: [ROLES.Admin],
  },
];
