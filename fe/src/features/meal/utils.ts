import dayjs, { type Dayjs } from 'dayjs';

/** Định dạng ngày API dùng (yyyy-MM-dd). */
export const toApiDate = (value: Dayjs): string => value.format('YYYY-MM-DD');

export const today = (): Dayjs => dayjs();

const currencyFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

/** Tiền theo chuẩn FE: "1.250.000 ₫" (Rules mục 1). */
export const formatCurrency = (value: number | null | undefined): string =>
  value === null || value === undefined ? '—' : `${currencyFormatter.format(value)} ₫`;
