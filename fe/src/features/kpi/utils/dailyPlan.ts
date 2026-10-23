import dayjs from 'dayjs';

const API_DATE = 'YYYY-MM-DD';
const SUNDAY = 0;

/** Tỉ lệ so với mức ổn định cho mấy ngày đầu vào đơn (tổ chưa thạo việc); các ngày sau 100%. */
export const RAMP_UP_RATES = [0.5, 0.7, 0.9] as const;

/** Các ngày từ `from` đến `to` (gồm hai đầu), mặc định bỏ Chủ nhật. */
export const listWorkDays = (from: string, to: string, skipSunday = true): string[] => {
  const days: string[] = [];
  const last = dayjs(to);
  for (let d = dayjs(from); !d.isAfter(last, 'day'); d = d.add(1, 'day')) {
    if (!skipSunday || d.day() !== SUNDAY) days.push(d.format(API_DATE));
  }
  return days;
};

/**
 * Chia `total` cho `count` ngày, mấy ngày đầu thấp rồi tăng dần (RAMP_UP_RATES) tới mức ổn định.
 * Số nguyên, mỗi ngày ≥ 1, tổng đúng bằng `total` (khi total ≥ count); phần dư dồn vào các ngày cuối.
 */
export const rampUp = (total: number, count: number): number[] => {
  if (count <= 0) return [];
  const weights = Array.from({ length: count }, (_, i) => RAMP_UP_RATES[i] ?? 1);
  const unit = total / weights.reduce((a, b) => a + b, 0);
  const result = weights.map((w) => Math.max(1, Math.floor(unit * w)));
  let diff = total - result.reduce((a, b) => a + b, 0);
  for (let i = count - 1; diff > 0; i = (i - 1 + count) % count) {
    result[i] = (result[i] ?? 0) + 1;
    diff -= 1;
  }
  for (let i = count - 1; diff < 0 && i >= 0; i -= 1) {
    const current = result[i] ?? 1;
    const take = Math.min(-diff, current - 1);
    result[i] = current - take;
    diff += take;
  }
  return result;
};

/** Chia đều, phần dư dồn vào các ngày cuối. */
export const evenSplit = (total: number, count: number): number[] => {
  if (count <= 0) return [];
  const base = Math.floor(total / count);
  const extra = total - base * count;
  return Array.from({ length: count }, (_, i) => base + (i >= count - extra ? 1 : 0));
};

/**
 * Lưới tháng kiểu lịch tờ: mỗi tuần 7 ô bắt đầu từ Thứ hai; ô thuộc tháng khác là null.
 * `month` là bất kỳ ngày nào trong tháng cần dựng.
 */
export const buildMonthGrid = (month: string): (string | null)[][] => {
  const first = dayjs(month).startOf('month');
  const lead = (first.day() + 6) % 7; // Thứ hai = 0 ... Chủ nhật = 6
  const cells: (string | null)[] = Array.from({ length: lead }, () => null);
  for (let d = first; d.month() === first.month(); d = d.add(1, 'day')) cells.push(d.format(API_DATE));
  while (cells.length % 7 !== 0) cells.push(null);
  const weeks: (string | null)[][] = [];
  for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7));
  return weeks;
};

/** Các tháng (đầu tháng, YYYY-MM-01) mà kỳ KPI trải qua. */
export const monthsOfPeriod = (from: string, to: string): string[] => {
  const months: string[] = [];
  const last = dayjs(to).startOf('month');
  for (let m = dayjs(from).startOf('month'); !m.isAfter(last, 'month'); m = m.add(1, 'month')) months.push(m.format(API_DATE));
  return months;
};
