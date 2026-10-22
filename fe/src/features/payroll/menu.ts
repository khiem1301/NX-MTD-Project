import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { PAYROLL_ROUTES } from './routes';
import { PAYROLL_TEXT } from './constants';

export const payrollMenu: MenuEntry[] = [
  {
    key: 'payroll',
    label: PAYROLL_TEXT.accountantPageTitle,
    path: PAYROLL_ROUTES.accountant,
    roles: [ROLES.Accountant],
  },
  {
    key: 'payroll-approval',
    label: PAYROLL_TEXT.adminPageTitle,
    path: PAYROLL_ROUTES.approval,
    roles: [ROLES.Admin],
  },
  {
    key: 'my-payslip',
    label: PAYROLL_TEXT.myPayslipPageTitle,
    path: PAYROLL_ROUTES.myPayslip,
    roles: 'all',
  },
  {
    key: 'my-estimated-pay',
    label: PAYROLL_TEXT.estimatedPay.pageTitle,
    path: PAYROLL_ROUTES.myEstimatedPay,
    roles: [ROLES.Worker],
  },
];
