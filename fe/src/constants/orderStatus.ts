import type { StatusLabel } from '@/components/common/StatusTag';

/** Trạng thái đơn hàng (CustomerOrder.Status), API trả PascalCase. Dùng chung vì luồng khác cần nhãn. */
export const ORDER_STATUS_LABEL = {
  New: 'Mới',
  InProduction: 'Đang sản xuất',
  InDelivery: 'Đang giao',
  PartiallyDelivered: 'Giao một phần',
  Delivered: 'Đã giao',
  Completed: 'Hoàn tất',
  Cancelled: 'Đã hủy',
  Stopped: 'Đã dừng',
} as const;
export type OrderStatus = keyof typeof ORDER_STATUS_LABEL;

const ORDER_STATUS_COLOR: Record<OrderStatus, string> = {
  New: 'blue',
  InProduction: 'gold',
  InDelivery: 'cyan',
  PartiallyDelivered: 'geekblue',
  Delivered: 'green',
  Completed: 'green',
  Cancelled: 'default',
  Stopped: 'red',
};

export const ORDER_STATUS_TAG: Record<string, StatusLabel> = Object.fromEntries(
  (Object.keys(ORDER_STATUS_LABEL) as OrderStatus[]).map((s) => [
    s,
    { text: ORDER_STATUS_LABEL[s], color: ORDER_STATUS_COLOR[s] },
  ]),
);

export const MATERIAL_STATUS_LABEL = { Awaiting: 'Chờ NVL', Partial: 'Có một phần', Complete: 'Đủ NVL' } as const;
export type OrderMaterialStatus = keyof typeof MATERIAL_STATUS_LABEL;

export const MATERIAL_STATUS_TAG: Record<string, StatusLabel> = {
  Awaiting: { text: MATERIAL_STATUS_LABEL.Awaiting, color: 'default' },
  Partial: { text: MATERIAL_STATUS_LABEL.Partial, color: 'orange' },
  Complete: { text: MATERIAL_STATUS_LABEL.Complete, color: 'green' },
};

/** Thứ tự cột size trên lưới; size khác xếp sau cùng theo chữ cái. */
export const SIZE_ORDER = ['XS', 'S', 'M', 'L', 'XL', 'XXL', '3XL', 'FREE'] as const;

/** D27: đơn sửa tự do khi Mới; đang sản xuất / giao chỉ sửa số lượng, đơn giá, thêm dòng. */
export const ORDER_LIMITED_EDIT_STATUSES: readonly OrderStatus[] = ['InProduction', 'InDelivery', 'PartiallyDelivered'];
