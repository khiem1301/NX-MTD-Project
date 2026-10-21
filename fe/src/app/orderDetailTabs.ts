import type { ComponentType } from 'react';

export type OrderDetailTab = { key: string; label: string; Component: ComponentType<{ orderId: number }> };

/** Điểm mở rộng của trang chi tiết đơn hàng: Thái (TH-02) thêm tab "NVL của đơn" vào đây bằng component của feature mình. */
export const ORDER_DETAIL_TABS: OrderDetailTab[] = [];
