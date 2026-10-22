import dayjs, { type Dayjs } from 'dayjs';

export type DateRange = [Dayjs, Dayjs];

/** Giờ hiển thị theo quy ước Rules FE: dd/MM/yyyy HH:mm, giờ Việt Nam. */
export const DATE_FORMAT = 'DD/MM/YYYY';
export const TIME_FORMAT = 'HH:mm';
export const DATE_TIME_FORMAT = 'DD/MM/YYYY HH:mm';

/** Định dạng ngày API dùng (yyyy-MM-dd), tránh lệch do múi giờ khi serialize. */
export const toApiDate = (value: Dayjs): string => value.format('YYYY-MM-DD');

export const formatDate = (value: string | null): string =>
  value ? dayjs(value).format(DATE_FORMAT) : '—';

export const formatTime = (value: string | null): string =>
  value ? dayjs(value).format(TIME_FORMAT) : '—';

export const formatDateTime = (value: string | null): string =>
  value ? dayjs(value).format(DATE_TIME_FORMAT) : '—';

/** Bỏ số 0 thừa: 1.00 → 1, 0.50 → 0.5. */
export const formatNumber = (value: number, maxDigits = 2): string =>
  new Intl.NumberFormat('vi-VN', { maximumFractionDigits: maxDigits }).format(value);

export const formatHours = (value: number): string => (value === 0 ? '—' : `${formatNumber(value)}h`);

/** Màu của ô công: đủ công xanh, thiếu công cam, không công xám. */
export const workdayTone = (value: number): 'full' | 'partial' | 'none' => {
  if (value >= 1) return 'full';
  if (value > 0) return 'partial';
  return 'none';
};

/** Kỳ xem mặc định khi mở màn hình: tháng hiện tại. */
export const defaultRange = (): DateRange => [dayjs().startOf('month'), dayjs().endOf('month')];
