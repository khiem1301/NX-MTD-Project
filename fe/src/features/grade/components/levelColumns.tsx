import { Tag, type TableColumnsType } from 'antd';
import dayjs from 'dayjs';
import { formatCurrency, formatDate } from '@/utils/format';
import { GR_LABELS } from '../constants';
import type { GradeLevel } from '../types';

/** Cột dùng chung của bảng lịch sử mức tiền: ngày hiệu lực, tiền, chênh lệch so với mức trước, trạng thái. */
export const levelColumns = (list: GradeLevel[]): TableColumnsType<GradeLevel> => {
  const previousOf = (row: GradeLevel) =>
    list
      .filter((l) => l.effectiveDate < row.effectiveDate)
      .sort((x, y) => y.effectiveDate.localeCompare(x.effectiveDate))[0];
  return [
    { title: GR_LABELS.effectiveDate, dataIndex: 'effectiveDate', render: formatDate },
    { title: GR_LABELS.amount, dataIndex: 'monthlyAmount', align: 'right', render: formatCurrency },
    {
      title: GR_LABELS.diffFromPrev,
      key: 'diff',
      align: 'right',
      render: (_, row) => {
        const prev = previousOf(row);
        const d = prev ? row.monthlyAmount - prev.monthlyAmount : 0;
        if (d === 0) return '—';
        return (
          <Tag
            color={d > 0 ? 'green' : 'red'}
          >{`${d > 0 ? '+' : '-'}${formatCurrency(Math.abs(d))}`}</Tag>
        );
      },
    },
    {
      title: GR_LABELS.status,
      dataIndex: 'isCurrent',
      render: (v: boolean, row) =>
        v ? (
          <Tag color="green">{GR_LABELS.current}</Tag>
        ) : dayjs(row.effectiveDate).isAfter(dayjs(), 'day') ? (
          <Tag color="blue">{GR_LABELS.upcoming}</Tag>
        ) : (
          <Tag>{GR_LABELS.expired}</Tag>
        ),
    },
  ];
};
