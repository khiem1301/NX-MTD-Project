import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ORDER_PAYMENT_ROUTES } from '@/constants/routes';

/** Thanh toán đơn hàng khách (DEV-ALL-118): chỉ Kế toán. */
export const orderPaymentRoutes = toPageRoutes([
  {
    path: ORDER_PAYMENT_ROUTES.list,
    roles: [ROLES.Accountant],
    page: async () => (await import('./pages/OrderPaymentsPage')).OrderPaymentsPage,
  },
]);
