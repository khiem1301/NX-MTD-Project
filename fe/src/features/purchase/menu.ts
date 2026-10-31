import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { PURCHASE_TEXT } from './constants';
import { PURCHASE_ROUTES } from './routes';

export const purchaseMenu: MenuEntry[] = [
  {
    key: 'suppliers',
    label: PURCHASE_TEXT.suppliersTitle,
    path: PURCHASE_ROUTES.suppliers,
    roles: [ROLES.Admin],
  },
  {
    key: 'purchase-orders',
    label: PURCHASE_TEXT.ordersTitle,
    path: PURCHASE_ROUTES.orders,
    roles: [ROLES.Manager],
  },
  {
    key: 'purchase-payments',
    label: PURCHASE_TEXT.paymentsTitle,
    path: PURCHASE_ROUTES.payments,
    roles: [ROLES.Accountant],
  },
];
