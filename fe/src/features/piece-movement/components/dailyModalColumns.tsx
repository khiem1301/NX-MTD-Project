import { InputNumber, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { fill } from '@/utils/format';
import { PM_LABELS } from '../constants';
import type { DailyAllocationRow } from '../types';
import { maxGiveOf } from '../utils/dailyBoard';

export type DailyTableRow =
  | {
      kind: 'group';
      key: string;
      groupKey: string;
      name: string | null;
      rows: DailyAllocationRow[];
    }
  | { kind: 'operation'; key: string; row: DailyAllocationRow };

type DailyModalColumnsOptions = {
  multiLine: boolean;
  groupQty: Record<string, number | null>;
  valueOf: (row: DailyAllocationRow) => number;
  onGroupChange: (groupKey: string, rows: DailyAllocationRow[], value: number | null) => void;
  onQuantityChange: (row: DailyAllocationRow, value: number | null) => void;
};

/** Cột khung giao việc một công nhân: dòng nhóm có ô điền cả nhóm, dòng công đoạn có ô sửa riêng. */
export const dailyModalColumns = ({
  multiLine,
  groupQty,
  valueOf,
  onGroupChange,
  onQuantityChange,
}: DailyModalColumnsOptions): ColumnsType<DailyTableRow> => [
  {
    title: PM_LABELS.operation,
    key: 'operation',
    ellipsis: true,
    render: (_, r) =>
      r.kind === 'group' ? (
        <Typography.Text strong>
          {fill(PM_LABELS.groupRow, r.name ?? PM_LABELS.noGroup, r.rows.length)}
        </Typography.Text>
      ) : (
        <>
          {multiLine && `${r.row.color}-${r.row.sizeCode} · `}
          {`${r.row.operationCode} - ${r.row.operationName}`}{' '}
          {!r.row.isAssigned && <Tag color="purple">{PM_LABELS.outsideAssignment}</Tag>}
        </>
      ),
  },
  {
    title: PM_LABELS.allocated,
    key: 'qty',
    width: 150,
    render: (_, r) =>
      r.kind === 'group' ? (
        <InputNumber
          aria-label={fill(PM_LABELS.groupInput, r.name ?? PM_LABELS.noGroup)}
          placeholder={PM_LABELS.groupPlaceholder}
          min={0}
          precision={0}
          style={{ width: 120 }}
          value={groupQty[r.groupKey] ?? null}
          onChange={(v) => onGroupChange(r.groupKey, r.rows, v)}
        />
      ) : (
        <InputNumber
          aria-label={fill(PM_LABELS.operationInput, r.row.operationCode)}
          min={r.row.outputQty}
          max={maxGiveOf(r.row)}
          precision={0}
          style={{ width: 120 }}
          value={valueOf(r.row)}
          onChange={(v) => onQuantityChange(r.row, v)}
        />
      ),
  },
  {
    title: PM_LABELS.output,
    key: 'output',
    width: 90,
    align: 'right',
    render: (_, r) => (r.kind === 'group' ? null : r.row.outputQty),
  },
  {
    title: PM_LABELS.unallocated,
    key: 'unallocated',
    width: 120,
    align: 'right',
    render: (_, r) => (r.kind === 'group' ? null : r.row.unallocatedQty),
  },
];
