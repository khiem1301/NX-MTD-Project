import { attendanceMenu } from '@/features/attendance/menu';
import { cuttingMenu } from '@/features/cutting';
import { profileMenu } from '@/features/profile';
import { teamMenu } from '@/features/teams';
import { userMenu } from '@/features/users';
import { orderMenu } from '@/features/orders';
import { requestMenu } from '@/features/requests';
import { warehouseMenu } from '@/features/warehouse';
import type { MenuEntry } from '@/types/menu';

const homeMenu: MenuEntry[] = [{ key: 'home', label: 'Trang chủ', path: '/', roles: 'all' }];

// Mỗi luồng thêm menu của mình ở features/<x>/menu.ts rồi thêm một dòng vào mảng dưới.
export const MENU_ENTRIES: MenuEntry[] = [
  ...homeMenu,
  // ...productionMenu,  // Kiều
  ...userMenu, // Khiêm
  ...teamMenu, // Khiêm
  ...orderMenu, // Khiêm
  ...requestMenu, // Khiêm
  ...warehouseMenu, // Thái
  ...cuttingMenu, // Thái
  ...attendanceMenu, // Minh
  ...profileMenu, // Khiêm
];
