import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ACCOUNTING_REPORT_ROUTES } from '@/constants/routes';

/** Báo cáo kế toán (Minh, MI-19, UC36, chỉ Kế toán). */
export const accountingReportRoutes = toPageRoutes([
  {
    path: ACCOUNTING_REPORT_ROUTES.accountant,
    roles: [ROLES.Accountant],
    page: async () => (await import('./pages/AccountingReportsPage')).AccountingReportsPage,
  },
]);
