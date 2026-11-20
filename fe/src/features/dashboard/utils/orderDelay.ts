import { ORDER_CLOSED_STATUSES, type OrderStatus } from '@/constants/orderStatus';
import { ORDER_DELAY } from '../constants';
import type { OrderProgress } from '../types';

export type OrderDelay = (typeof ORDER_DELAY)[keyof typeof ORDER_DELAY];

/**
 * Đơn đã quá ngày giao mà chưa đóng (chưa giao xong, chưa hủy / dừng) → trễ hạn. Còn hạn nhưng hệ thống dự báo không kịp
 * (isDelayRisk) → nguy cơ trễ. Đơn đã đóng thì không cảnh báo.
 */
export function orderDelayOf(
  o: Pick<OrderProgress, 'daysLeft' | 'isDelayRisk' | 'status'>,
): OrderDelay | null {
  if (ORDER_CLOSED_STATUSES.includes(o.status as OrderStatus)) return null;
  if (o.daysLeft < 0) return ORDER_DELAY.LATE;
  return o.isDelayRisk ? ORDER_DELAY.RISK : null;
}
