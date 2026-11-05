import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { PAYROLL_ROUTES } from '@/constants/routes';

/** Bảng lương (Minh). Phiếu lương của tôi: mọi vai trò. */
export const payrollRoutes = toPageRoutes([
  {
    path: PAYROLL_ROUTES.myPayslip,
    page: async () => (await import('./pages/MyPayslipPage')).MyPayslipPage,
  },
  {
    path: PAYROLL_ROUTES.myEstimatedPay,
    roles: [ROLES.Worker],
    page: async () => (await import('./pages/MyEstimatedPayPage')).MyEstimatedPayPage,
  },
  {
    path: PAYROLL_ROUTES.accountant,
    roles: [ROLES.Accountant],
    page: async () => (await import('./pages/AccountantPayrollPage')).AccountantPayrollPage,
  },
  {
    path: PAYROLL_ROUTES.approval,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/PayrollApprovalPage')).PayrollApprovalPage,
  },
]);
