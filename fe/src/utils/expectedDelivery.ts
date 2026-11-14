import dayjs from 'dayjs';
import type { Dayjs } from 'dayjs';
import { MAX_DELIVERY_DATES } from '@/constants/delivery';
import { formatDate, toApiDate } from '@/utils/format';

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

/** Số ô ngày giao cần hiện: bằng số lần giao, trong khoảng 0 đến MAX_DELIVERY_DATES. */
export const deliveryDateSlots = (count: number | null | undefined): number =>
  Math.min(Math.max(Math.trunc(count ?? 0), 0), MAX_DELIVERY_DATES);

/** Ngày giao gửi API: đúng `count` ngày đầu của danh sách đã nhập (ô trống bị bỏ). */
export const toDeliveryDatesPayload = (
  count: number | null | undefined,
  dates: (Dayjs | null | undefined)[] | undefined,
): string[] =>
  (dates ?? [])
    .slice(0, deliveryDateSlots(count))
    .filter((d): d is Dayjs => Boolean(d))
    .map(toApiDate);

/**
 * Ngày giao điền sẵn vào form: danh sách đã lưu; dữ liệu cũ chưa có danh sách thì dùng ngày giao đầu / cuối
 * (từ – đến) để người dùng không phải nhập lại từ đầu.
 */
export const toDeliveryDateValues = (
  dates: string[] | null | undefined,
  from?: string | null,
  to?: string | null,
): Dayjs[] =>
  (dates?.length ? dates : [from, to].filter((d): d is string => Boolean(d))).map((d) => dayjs(d));

/** Hiển thị ngày dự kiến giao: các ngày cách nhau dấu phẩy; chưa có danh sách thì "từ – đến" như cũ. */
export const formatDeliveryDates = (
  dates?: string[] | null,
  from?: string | null,
  to?: string | null,
): string => {
  if (dates?.length) return dates.map((d) => formatDate(d)).join(', ');
  return to ? `${formatDate(from)} – ${formatDate(to)}` : formatDate(from);
};

/** Ngày giao cuối của lịch: ngày cuối danh sách, chưa có danh sách thì ngày giao đến / ngày giao đầu. */
export const lastDeliveryDate = (
  dates?: string[] | null,
  from?: string | null,
  to?: string | null,
): string | null => dates?.at(-1) ?? to ?? from ?? null;
