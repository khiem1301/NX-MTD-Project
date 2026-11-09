import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';

/**
 * Ngày dự kiến giao không được trước hôm nay, không trước ngày đặt hàng (nếu có) và không sau hạn giao của đơn hàng
 * (nếu biết). BE kiểm lại, đây chỉ để chặn chọn sai ngay trên lịch.
 */
export const isExpectedDeliveryDateDisabled = (
  date: Dayjs,
  notBefore?: Dayjs | null,
  dueDate?: string | null,
): boolean =>
  date.isBefore(dayjs(), 'day') ||
  (notBefore ? date.isBefore(notBefore, 'day') : false) ||
  (dueDate ? date.isAfter(dayjs(dueDate), 'day') : false);
