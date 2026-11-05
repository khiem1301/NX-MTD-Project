import { Tag } from 'antd';
import dayjs from 'dayjs';
import { fill } from '@/utils/format';
import { KPI_TEXT } from '../constants';

const text = KPI_TEXT.form;

/** Nhãn số ngày còn tới ngày giao hàng. */
export function DueTag({ dueDate }: { dueDate: string }) {
  const daysLeft = dayjs(dueDate).diff(dayjs().startOf('day'), 'day');
  if (daysLeft < 0) return <Tag color="red">{fill(text.dueOverdue, -daysLeft)}</Tag>;
  return daysLeft === 0 ? (
    <Tag color="orange">{text.dueToday}</Tag>
  ) : (
    <Tag>{fill(text.dueDaysLeft, daysLeft)}</Tag>
  );
}
