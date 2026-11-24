import { lazy } from 'react';

/**
 * Điểm mở rộng: component của một feature được feature khác dùng (Coding Rules 4.3 cấm feature import chéo nhau).
 * Gom ở tầng app như ORDER_DETAIL_TABS; nạp lười để màn dùng không kéo cả feature kia vào gói.
 */

/** Định mức mã hàng (orders) — thêm NVL mới ngay trên dòng định mức (DEV-ALL-61). */
export const MaterialFormModalSlot = lazy(() =>
  import('@/features/warehouse').then((m) => ({ default: m.MaterialFormModal })),
);

/** Đơn mua (purchase) — Quản đốc lập đơn mua NVL bằng form của feature kho. */
export const PurchaseOrderFormModalSlot = lazy(() =>
  import('@/features/warehouse').then((m) => ({ default: m.PurchaseOrderFormModal })),
);

/** Thông báo (notification) — bấm thông báo lệch kiểm cuối đơn mở ngay cửa sổ chi tiết của feature KPI, không chuyển trang. */
export const KpiMismatchModalSlot = lazy(() =>
  import('@/features/kpi').then((m) => ({ default: m.MismatchDetailModal })),
);
