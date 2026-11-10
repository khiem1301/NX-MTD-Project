import { lazy } from 'react';

/**
 * Điểm mở rộng: component của một feature được feature khác dùng (Coding Rules 4.3 cấm feature import chéo nhau).
 * Gom ở tầng app như ORDER_DETAIL_TABS; nạp lười để màn dùng không kéo cả feature kia vào gói.
 */

/** Trang chủ (dashboard) — doanh thu cho Admin. */
export const RevenuePanelSlot = lazy(() =>
  import('@/features/revenue').then((m) => ({ default: m.RevenuePanel })),
);

/** Trang chủ (dashboard) — KPI của tổ may cho tổ trưởng / tổ phó. */
export const TeamKpiPanelSlot = lazy(() =>
  import('@/features/kpi').then((m) => ({ default: m.TeamKpiPanel })),
);

/** Định mức mã hàng (orders) — thêm NVL mới ngay trên dòng định mức (DEV-ALL-61). */
export const MaterialFormModalSlot = lazy(() =>
  import('@/features/warehouse').then((m) => ({ default: m.MaterialFormModal })),
);

/** Đơn mua (purchase) — Quản đốc lập đơn mua NVL bằng form của feature kho. */
export const PurchaseOrderFormModalSlot = lazy(() =>
  import('@/features/warehouse').then((m) => ({ default: m.PurchaseOrderFormModal })),
);
