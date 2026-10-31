import dayjs, { type Dayjs } from 'dayjs';
import { formatDate, formatDateTime, formatTime, toApiDate } from '@/utils/format';

export { DATE_FORMAT, DATE_TIME_FORMAT, TIME_FORMAT } from '@/utils/format';
export { formatDate, formatDateTime, formatTime, toApiDate };

export type DateRange = [Dayjs, Dayjs];

/** Bỏ số 0 thừa: 1.00 → 1, 0.50 → 0.5. */
export const formatNumber = (value: number, maxDigits = 2): string =>
  new Intl.NumberFormat('vi-VN', { maximumFractionDigits: maxDigits }).format(value);

export const formatHours = (value: number): string =>
  value === 0 ? '—' : `${formatNumber(value)}h`;

/** Màu của ô công: đủ công xanh, thiếu công cam, không công xám. */
export const workdayTone = (value: number): 'full' | 'partial' | 'none' => {
  if (value >= 1) return 'full';
  if (value > 0) return 'partial';
  return 'none';
};

/** Kỳ xem mặc định khi mở màn hình: tháng hiện tại. */
export const defaultRange = (): DateRange => [dayjs().startOf('month'), dayjs().endOf('month')];
