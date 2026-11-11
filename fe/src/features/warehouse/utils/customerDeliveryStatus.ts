import dayjs from 'dayjs';
import type { OrderCustomerDelivery } from '../types';

export const CUSTOMER_DELIVERY_STATUS = {
  /** Đã nhận đủ số lần khách giao: thêm / tăng NVL khách cấp bị chặn cho tới khi tăng số lần. */
  FULL: 'FULL',
  /** Còn lượt giao nhưng đã qua ngày giao cuối của lịch. */
  LATE: 'LATE',
} as const;

export type CustomerDeliveryStatus =
  (typeof CUSTOMER_DELIVERY_STATUS)[keyof typeof CUSTOMER_DELIVERY_STATUS];

/**
 * Tình trạng lịch khách giao của cả đơn (DEV-ALL-95): đủ số lần (chưa khai coi như 1 lần), hoặc quá ngày giao cuối (ngày giao đến,
 * giao 1 lần thì ngày giao); null nếu bình thường hoặc đơn không có NVL khách cấp.
 */
export const customerDeliveryStatus = (
  plan: OrderCustomerDelivery | undefined,
  today = dayjs(),
): CustomerDeliveryStatus | null => {
  if (!plan?.hasCustomerMaterials) return null; // khách không cấp NVL thì không có lịch giao để nhắc
  if (plan.receivedCount >= (plan.deliveryCount ?? 1)) return CUSTOMER_DELIVERY_STATUS.FULL;
  const lastDate = plan.deliveryDateTo ?? plan.deliveryDate;
  return lastDate && dayjs(lastDate).isBefore(today, 'day') ? CUSTOMER_DELIVERY_STATUS.LATE : null;
};
