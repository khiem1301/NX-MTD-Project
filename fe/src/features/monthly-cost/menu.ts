import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { MONTHLY_COST_ROUTES } from './routes';
import { MONTHLY_COST_TEXT } from './constants';

export const monthlyCostMenu: MenuEntry[] = [
  {
    key: 'monthly-cost',
    label: MONTHLY_COST_TEXT.pageTitle,
    path: MONTHLY_COST_ROUTES.accountant,
    roles: [ROLES.Accountant],
  },
];
