import { toPageRoutes } from '@/components/guards/pageRoutes';
import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';

/** Mã hàng, đơn hàng (Khiêm). Admin: đầy đủ vì có đơn giá CMT (D26); Quản đốc chỉ xem đơn (GBR-02); Kế toán nhập định mức NVL của mã hàng. */
export const orderRoutes = toPageRoutes([
  {
    path: ROUTES.orders,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/OrdersPage')).OrdersPage,
  },
  {
    path: ROUTES.orderNew,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/OrderFormPage')).OrderFormPage,
  },
  {
    path: `${ROUTES.orders}/:id`,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/OrderDetailPage')).OrderDetailPage,
  },
  {
    path: `${ROUTES.orders}/:id/edit`,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/OrderFormPage')).OrderFormPage,
  },
  {
    path: ROUTES.products,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/ProductsPage')).ProductsPage,
  },
  {
    path: ROUTES.customers,
    roles: [ROLES.Admin],
    page: async () => (await import('./pages/CustomersPage')).CustomersPage,
  },
  {
    path: ROUTES.managerOrders,
    roles: [ROLES.Manager],
    page: async () => (await import('./pages/OrderOverviewListPage')).OrderOverviewListPage,
  },
  {
    path: `${ROUTES.managerOrders}/:id`,
    roles: [ROLES.Manager],
    page: async () => (await import('./pages/OrderOverviewPage')).OrderOverviewPage,
  },
  {
    path: ROUTES.accountantProducts,
    roles: [ROLES.Accountant],
    page: async () => (await import('./pages/ProductsPage')).ProductsPage,
  },
]);
