import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { COST_SUMMARY_ROUTES } from './routes';
import { COST_SUMMARY_TEXT } from './constants';

/** UC12 — Tổng hợp chi phí: CHỈ Admin (dữ liệu "mật", GBR-02). */
export const costSummaryMenu: MenuEntry[] = [
  {
    key: 'cost-summary',
    label: COST_SUMMARY_TEXT.pageTitle,
    path: COST_SUMMARY_ROUTES.admin,
    roles: [ROLES.Admin],
  },
];
