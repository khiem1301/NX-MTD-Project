import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

const { Admin } = ROLES;

export const orderMenu: MenuEntry[] = [
  { key: 'orders', label: 'Đơn hàng', path: ROUTES.orders, roles: [Admin] },
  { key: 'products', label: 'Mã hàng', path: ROUTES.products, roles: [Admin] },
  { key: 'customers', label: 'Khách hàng', path: ROUTES.customers, roles: [Admin] },
];
