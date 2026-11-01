import dayjs from 'dayjs';

export type DueWarning = { kind: 'after' | 'near'; days: number };

/** Kỳ KPI kết thúc sau ngày giao, hoặc cách ngày giao không quá warningDays ngày; null là không cần cảnh báo. */
export function dueWarning(
  endDate: string,
  dueDate: string,
  warningDays: number,
): DueWarning | null {
  const daysLeft = dayjs(dueDate).diff(dayjs(endDate), 'day');
  if (daysLeft < 0) return { kind: 'after', days: -daysLeft };
  return daysLeft <= warningDays ? { kind: 'near', days: daysLeft } : null;
}
