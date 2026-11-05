import { accountingReportsMenu } from '@/features/accounting-reports/menu';
import { attendanceMenu } from '@/features/attendance/menu';
import { auditLogMenu } from '@/features/audit-log/menu';
import { breakEvenMenu } from '@/features/break-even/menu';
import { costSummaryMenu } from '@/features/cost-summary/menu';
import { cuttingMenu } from '@/features/cutting/menu';
import { dispatchMenu } from '@/features/dispatch/menu';
import { fgHandoverMenu } from '@/features/fg-handover/menu';
import { productionMenu } from '@/features/production/menu';
import { pieceMovementMenu } from '@/features/piece-movement/menu';
import { profileMenu } from '@/features/profile/menu';
import { teamMenu } from '@/features/teams/menu';
import { userMenu } from '@/features/users/menu';
import { notificationMenu } from '@/features/notification/menu';
import { orderMenu } from '@/features/orders/menu';
import { requestMenu } from '@/features/requests/menu';
import { gradeMenu } from '@/features/grade/menu';
import { kpiMenu } from '@/features/kpi/menu';
import { materialReturnMenu } from '@/features/material-return/menu';
import { mealMenu } from '@/features/meal/menu';
import { monthlyCostMenu } from '@/features/monthly-cost/menu';
import { payrollMenu } from '@/features/payroll/menu';
import { purchaseMenu } from '@/features/purchase/menu';
import { revenueMenu } from '@/features/revenue/menu';
import { stocktakeMenu } from '@/features/stocktake/menu';
import { vehicleLogMenu } from '@/features/vehicle-log/menu';
import { warehouseMenu } from '@/features/warehouse/menu';
import type { MenuEntry } from '@/types/menu';

const homeMenu: MenuEntry[] = [{ key: 'home', label: 'Trang chủ', path: '/', roles: 'all' }];

// Mỗi luồng thêm menu của mình ở features/<x>/menu.ts rồi thêm một dòng vào mảng dưới.
// Chỉ import menu.ts của feature: import index.ts sẽ kéo cả feature vào bundle chính.
export const MENU_ENTRIES: MenuEntry[] = [
  ...homeMenu,
  ...notificationMenu, // Khiêm (KH-12)
  ...gradeMenu, // Khiêm (KH-14)
  ...auditLogMenu, // Khiêm (KH-22)
  ...productionMenu, // Minh làm hộ Kiều (KI-01..KI-11)
  ...userMenu, // Khiêm
  ...teamMenu, // Khiêm
  ...orderMenu, // Khiêm
  ...requestMenu, // Khiêm
  ...warehouseMenu, // Thái
  ...cuttingMenu, // Thái
  ...fgHandoverMenu, // Thái (TH-11)
  ...dispatchMenu, // Thái (TH-14)
  ...stocktakeMenu, // Thảo (TA-02)
  ...vehicleLogMenu, // Thảo (TA-03)
  ...materialReturnMenu, // Thái (TH-10)
  ...pieceMovementMenu, // Thái (TH-19)
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
