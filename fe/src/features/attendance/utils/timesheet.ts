import dayjs, { type Dayjs } from 'dayjs';
import { formatNumber } from '@/utils/format';

export type DateRange = [Dayjs, Dayjs];

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
