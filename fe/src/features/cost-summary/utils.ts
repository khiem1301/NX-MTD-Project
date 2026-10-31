import dayjs, { type Dayjs } from 'dayjs';
import { API_MONTH, toApiMonth } from '@/utils/format';

const currencyFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

/** Tiền theo chuẩn FE: "1.250.000 ₫" (Rules mục 1). */
export const formatCurrency = (value: number | null | undefined): string =>
  value === null || value === undefined ? '—' : `${currencyFormatter.format(value)} ₫`;

/** BE nhận/trả periodMonth dạng "yyyy-MM". */
export const PERIOD_MONTH_FORMAT = API_MONTH;

export const toPeriodMonth = toApiMonth;

export const currentPeriodMonth = (): Dayjs => dayjs();
