import type { Dayjs } from 'dayjs';

const VN_OFFSET_MS = 7 * 60 * 60 * 1000;
const EMPTY = '—';
const qtyFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 3 });

/** Số lượng: 1.250,5 (tối đa 3 chữ số thập phân như DB decimal(18,3)). */
export const formatQty = (value: number | null | undefined): string =>
  value === null || value === undefined ? '—' : qtyFormatter.format(value);

const pad = (n: string | undefined): string => (n ?? '').padStart(2, '0');

/** "2026-10-20" → "20/10/2026". BE trả ngày dạng DateOnly nên không đổi múi giờ. */
export const formatDate = (value: string | null | undefined): string => {
  if (!value) return '—';
  const [y, m, d] = value.slice(0, 10).split('-');
  return y && m && d ? `${pad(d)}/${pad(m)}/${y}` : value;
};

/** "2026-10-20T08:05:00" → "20/10/2026 08:05". Giờ BE đã là giờ Việt Nam (fn_VnNow). */
export const formatDateTime = (value: string | null | undefined): string => {
  if (!value) return '—';
  // Chuỗi UTC ("...Z") đổi về giờ VN; chuỗi "+07:00" hoặc không offset đã là giờ VN (D21).
  if (value.endsWith('Z')) {
    const vn = new Date(Date.parse(value) + VN_OFFSET_MS);
    const iso = vn.toISOString(); // đọc các trường UTC của thời điểm đã cộng 7 giờ
    return `${formatDate(iso)} ${iso.slice(11, 16)}`;
  }
  const time = value.slice(11, 16);
  return time ? `${formatDate(value)} ${time}` : formatDate(value);
};

const currencyFormatter = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

/** 1250000 → "1.250.000 ₫" */
export const formatCurrency = (value: number | null | undefined): string =>
  value === null || value === undefined ? EMPTY : `${currencyFormatter.format(Math.round(value))} ₫`;

/** Định dạng hiển thị và nhập liệu duy nhất cho toàn hệ thống (Rules FE: dd/MM/yyyy HH:mm). */
export const DATE_FORMAT = 'DD/MM/YYYY';
export const DATE_TIME_FORMAT = 'DD/MM/YYYY HH:mm';
export const TIME_FORMAT = 'HH:mm';
export const MONTH_FORMAT = 'MM/YYYY';

/** Định dạng gửi lên API. */
export const API_DATE = 'YYYY-MM-DD';
export const API_MONTH = 'YYYY-MM';

/** Giá trị gửi lên API cho cột DATE. */
export const toApiDate = (value: Dayjs): string => value.format(API_DATE);

/** Giá trị gửi lên API cho kỳ tháng. */
export const toApiMonth = (value: Dayjs): string => value.format(API_MONTH);

/** "2026-10" → "10/2026". */
export const formatMonth = (value: string | null | undefined): string => {
  if (!value) return EMPTY;
  const [y, m] = value.slice(0, 7).split('-');
  return y && m ? `${pad(m)}/${y}` : value;
};

/** "08:05:00" hoặc "2026-10-20T08:05:00" → "08:05". */
export const formatTime = (value: string | null | undefined): string => {
  if (!value) return EMPTY;
  return value.includes('T') ? value.slice(11, 16) : value.slice(0, 5);
};

/** fill('Đã đủ {0} ảnh', 5) → 'Đã đủ 5 ảnh'. */
export const fill = (template: string, ...args: (string | number)[]): string =>
  args.reduce<string>((text, v, i) => text.replace(`{${i}}`, String(v)), template);
