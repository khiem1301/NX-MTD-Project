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
  const time = value.slice(11, 16);
  return time ? `${formatDate(value)} ${time}` : formatDate(value);
};
