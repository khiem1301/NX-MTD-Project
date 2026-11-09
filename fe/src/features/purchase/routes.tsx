import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { PURCHASE_ROUTES } from '@/constants/routes';

/** Nhà cung cấp + đơn mua + thanh toán (Minh, MI-07). */
export const purchaseRoutes = toPageRoutes([
  {
    path: PURCHASE_ROUTES.suppliers,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/SuppliersPage')).SuppliersPage,
  },
  {
    path: PURCHASE_ROUTES.orders,
    roles: [ROLES.Admin, ROLES.Manager],
    page: async () => (await import('./pages/PurchaseOrdersPage')).PurchaseOrdersPage,
  },
  {
    path: PURCHASE_ROUTES.payments,
    roles: [ROLES.Accountant],
    page: async () => (await import('./pages/PurchasePaymentsPage')).PurchasePaymentsPage,
  },
]);
