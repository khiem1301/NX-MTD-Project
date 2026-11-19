import { ROLES } from '@/constants/roles';
import { ORDER_PAYMENT_ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';
import { ORDER_PAYMENT_TEXT } from './constants';

export const orderPaymentMenu: MenuEntry[] = [
  {
    key: 'order-payments',
    label: ORDER_PAYMENT_TEXT.title,
    path: ORDER_PAYMENT_ROUTES.list,
    roles: [ROLES.Accountant],
  },
];
