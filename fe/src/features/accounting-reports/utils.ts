import dayjs, { type Dayjs } from 'dayjs';

const currencyFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });
const numberFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 });

/** Tiền theo chuẩn FE: "1.250.000 ₫" (Rules mục 1). */
export const formatCurrency = (value: number | null | undefined): string =>
  value === null || value === undefined ? '—' : `${currencyFormatter.format(value)} ₫`;

/** Số công/giờ (có thể lẻ, VD 0.5 công) — tối đa 2 chữ số thập phân. */
export const formatNumber = (value: number): string => numberFormatter.format(value);

/** BE nhận/trả periodMonth dạng "yyyy-MM". */
export const PERIOD_MONTH_FORMAT = 'YYYY-MM';

export const toPeriodMonth = (value: Dayjs): string => value.format(PERIOD_MONTH_FORMAT);

export const currentPeriodMonth = (): Dayjs => dayjs();
