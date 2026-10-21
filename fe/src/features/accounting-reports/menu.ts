import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { ACCOUNTING_REPORT_ROUTES } from './routes';
import { ACCOUNTING_REPORT_TEXT } from './constants';

/** UC36 — Báo cáo kế toán: CHỈ Kế toán (permission matrix: Accountant Full, Admin/Manager/WarehouseKeeper No). */
export const accountingReportsMenu: MenuEntry[] = [
  {
    key: 'accounting-reports',
    label: ACCOUNTING_REPORT_TEXT.pageTitle,
    path: ACCOUNTING_REPORT_ROUTES.accountant,
    roles: [ROLES.Accountant],
  },
];
