import dayjs from 'dayjs';

/** Một dòng chỉ tiêu ngày trong form (targetQty null = chưa nhập). */
export type DailyRow = { workDate: string; targetQty: number | null };

const API_DATE = 'YYYY-MM-DD';
const SUNDAY = 0;

/** Tỉ lệ so với mức ổn định cho mấy ngày đầu vào đơn (tổ chưa thạo việc); các ngày sau 100%. */
export const RAMP_UP_RATES = [0.5, 0.7, 0.9] as const;

/** Các ngày trong kỳ (gồm cả hai đầu), mặc định bỏ Chủ nhật. */
export const listWorkDays = (start: string, end: string, skipSunday = true): string[] => {
  const days: string[] = [];
  const last = dayjs(end);
  for (let d = dayjs(start); !d.isAfter(last, 'day'); d = d.add(1, 'day')) {
    if (!skipSunday || d.day() !== SUNDAY) days.push(d.format(API_DATE));
  }
  return days;
};

/** Dựng lại danh sách dòng theo các ngày mới, giữ số đã nhập của ngày vẫn còn trong kỳ. */
export const syncRows = (workDates: string[], current: DailyRow[]): DailyRow[] => {
  const kept = new Map(current.map((r) => [r.workDate, r.targetQty]));
  return workDates.map((workDate) => ({ workDate, targetQty: kept.get(workDate) ?? null }));
};

/** Chỉ giữ các dòng nằm trong kỳ mới (dùng khi đổi kỳ mà vẫn muốn giữ ngày đã thêm tay, ví dụ Chủ nhật tăng ca). */
export const rowsForPeriod = (start: string, end: string, current: DailyRow[]): DailyRow[] => {
  const extra = current.filter((r) => r.workDate >= start && r.workDate <= end).map((r) => r.workDate);
  const dates = [...new Set([...listWorkDays(start, end), ...extra])].sort();
  return syncRows(dates, current);
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

/** Chia đều (dùng khi mở sửa một KPI cũ chưa có chỉ tiêu từng ngày). */
export const evenSplit = (total: number, count: number): number[] => {
  if (count <= 0) return [];
  const base = Math.floor(total / count);
  return Array.from({ length: count }, (_, i) => base + (i >= count - (total - base * count) ? 1 : 0));
};

export const sumRows = (rows: DailyRow[]): number => rows.reduce((s, r) => s + (r.targetQty ?? 0), 0);
