import { ROLES } from '@/constants/roles';
import { ROUTES } from '@/constants/routes';
import type { MenuEntry } from '@/types/menu';

const { Admin, Accountant } = ROLES;

export const orderMenu: MenuEntry[] = [
  { key: 'orders', label: 'Đơn hàng', path: ROUTES.orders, roles: [Admin] },
  { key: 'products', label: 'Mã hàng', path: ROUTES.products, roles: [Admin] },
  { key: 'customers', label: 'Khách hàng', path: ROUTES.customers, roles: [Admin] },
  // Kế toán nhập thành phần và định mức NVL của mã hàng (cùng Admin); chỉ xem danh sách + sửa định mức.
  {
    key: 'products-accountant',
    label: 'Mã hàng',
    path: ROUTES.accountantProducts,
    roles: [Accountant],
  },
];
