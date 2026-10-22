import { accountingReportsMenu } from '@/features/accounting-reports/menu';
import { attendanceMenu } from '@/features/attendance/menu';
import { breakEvenMenu } from '@/features/break-even/menu';
import { costSummaryMenu } from '@/features/cost-summary/menu';
import { cuttingMenu } from '@/features/cutting';
import { dispatchMenu } from '@/features/dispatch';
import { fgHandoverMenu } from '@/features/fg-handover';
import { productionMenu } from '@/features/production/menu';
import { profileMenu } from '@/features/profile';
import { teamMenu } from '@/features/teams';
import { userMenu } from '@/features/users';
import { orderMenu } from '@/features/orders';
import { requestMenu } from '@/features/requests';
import { kpiMenu } from '@/features/kpi/menu';
import { mealMenu } from '@/features/meal/menu';
import { monthlyCostMenu } from '@/features/monthly-cost/menu';
import { payrollMenu } from '@/features/payroll/menu';
import { purchaseMenu } from '@/features/purchase/menu';
import { revenueMenu } from '@/features/revenue/menu';
import { warehouseMenu } from '@/features/warehouse';
import type { MenuEntry } from '@/types/menu';

const homeMenu: MenuEntry[] = [{ key: 'home', label: 'Trang chủ', path: '/', roles: 'all' }];

// Mỗi luồng thêm menu của mình ở features/<x>/menu.ts rồi thêm một dòng vào mảng dưới.
export const MENU_ENTRIES: MenuEntry[] = [
  ...homeMenu,
  ...productionMenu, // Minh làm hộ Kiều (KI-01..KI-11)
  ...userMenu, // Khiêm
  ...teamMenu, // Khiêm
  ...orderMenu, // Khiêm
  ...requestMenu, // Khiêm
  ...warehouseMenu, // Thái
  ...cuttingMenu, // Thái
  ...fgHandoverMenu, // Thái (TH-11)
  ...dispatchMenu, // Thái (TH-14)
  ...attendanceMenu, // Minh
  ...profileMenu, // Khiêm
  ...mealMenu, // Minh
  ...payrollMenu, // Minh
  ...purchaseMenu, // Minh (MI-07)
  ...monthlyCostMenu, // Minh
  ...kpiMenu, // Minh
  ...revenueMenu, // Minh
  ...breakEvenMenu, // Minh
  ...costSummaryMenu, // Minh (MI-19, UC12, chỉ Admin)
  ...accountingReportsMenu, // Minh (MI-19, UC36, chỉ Kế toán)
];
