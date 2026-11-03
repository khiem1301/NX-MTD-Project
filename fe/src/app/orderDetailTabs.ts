import { lazy, type ComponentType } from 'react';
import { MESSAGES } from '@/constants/messages';
import { ORDER_MATERIALS_TAB } from '@/constants/orderStatus';

export type OrderDetailTabProps = {
  orderId: number;
  /** Đơn đã đóng: tab chỉ đọc. */
  isClosed: boolean;
};

export type OrderDetailTab = {
  key: string;
  label: string;
  Component: ComponentType<OrderDetailTabProps>;
};

/** Điểm mở rộng của trang chi tiết đơn hàng: feature khác gắn tab vào đây; nạp lười để trang đơn không kéo cả feature kho. */
export const ORDER_DETAIL_TABS: OrderDetailTab[] = [
  {
    key: ORDER_MATERIALS_TAB,
    label: MESSAGES.orders.materialsTab,
    Component: lazy(() =>
      import('@/features/warehouse').then((m) => ({ default: m.OrderMaterialsTab })),
    ),
  },
];
