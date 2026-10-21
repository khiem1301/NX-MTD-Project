import dayjs, { type Dayjs } from 'dayjs';

const currencyFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

/** Tiền theo chuẩn FE: "1.250.000 ₫" (Rules mục 1). */
export const formatCurrency = (value: number | null | undefined): string =>
  value === null || value === undefined ? '—' : `${currencyFormatter.format(value)} ₫`;

/** BE nhận/trả periodMonth dạng "yyyy-MM" (PeriodMonthPattern của MonthlyCostService). */
export const PERIOD_MONTH_FORMAT = 'YYYY-MM';

export const toPeriodMonth = (value: Dayjs): string => value.format(PERIOD_MONTH_FORMAT);

export const currentPeriodMonth = (): Dayjs => dayjs();
