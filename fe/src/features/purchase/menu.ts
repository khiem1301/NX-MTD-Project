import { ROLES } from '@/constants/roles';
import type { MenuEntry } from '@/types/menu';
import { PURCHASE_ROUTES } from '@/constants/routes';
import { PURCHASE_TEXT } from './constants';

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
    roles: [ROLES.Admin, ROLES.Manager],
  },
  {
    key: 'purchase-payments',
    label: PURCHASE_TEXT.paymentsTitle,
    path: PURCHASE_ROUTES.payments,
    roles: [ROLES.Accountant],
  },
];
