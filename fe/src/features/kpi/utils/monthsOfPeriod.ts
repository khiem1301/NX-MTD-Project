import dayjs from 'dayjs';
import { API_DATE } from '@/utils/format';

/** Ngày đầu của từng tháng mà kỳ KPI đi qua (YYYY-MM-DD), để chuyển tháng trên lịch. */
export function monthsOfPeriod(startDate: string, endDate: string): string[] {
  const months: string[] = [];
  const end = dayjs(endDate);
  for (let m = dayjs(startDate).startOf('month'); !m.isAfter(end); m = m.add(1, 'month'))
    months.push(m.format(API_DATE));
  return months;
}
