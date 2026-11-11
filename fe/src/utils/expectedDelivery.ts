import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { RANGE_DELIVERY_COUNT } from '@/constants/delivery';
import { formatDate } from '@/utils/format';

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

/** Giao từ 2 lần trở lên thì nhập khoảng ngày giao (từ ngày – đến ngày). */
export const isRangeDelivery = (count: number | null | undefined): boolean =>
  (count ?? 0) >= RANGE_DELIVERY_COUNT;

/** Ngày giao đến gửi API: chỉ khi giao từ 2 lần trở lên. */
export const deliveryDateTo = <T>(
  count: number | null | undefined,
  dateTo: T | null | undefined,
) => (isRangeDelivery(count) ? (dateTo ?? null) : null);

/** Hiển thị ngày dự kiến giao: một ngày, hoặc "từ – đến" khi có ngày giao đến. */
export const formatDeliveryWindow = (from?: string | null, to?: string | null): string =>
  to ? `${formatDate(from)} – ${formatDate(to)}` : formatDate(from);
