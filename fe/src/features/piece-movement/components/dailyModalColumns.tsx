import type { MouseEvent } from 'react';
import { DownOutlined, RightOutlined } from '@ant-design/icons';
import { Space, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { fill, formatCurrency } from '@/utils/format';
import { QtyInput } from '@/components/common/QtyInput';
import { PM_LABELS } from '../constants';
import type { DailyAllocationRow } from '../types';
import { maxGiveOf, minGiveOf } from '../utils/dailyBoard';

export type DailyTableRow =
  | {
      kind: 'group';
      key: string;
      groupKey: string;
      name: string | null;
      rows: DailyAllocationRow[];
      isOpen: boolean;
      /** Số công đoạn trong nhóm đã sửa mà chưa lưu, để thấy được cả khi nhóm đang đóng. */
      changedCount: number;
    }
  | { kind: 'operation'; key: string; row: DailyAllocationRow };

type DailyModalColumnsOptions = {
  /** false = ngày đã qua, chỉ xem. */
  editable: boolean;
  multiLine: boolean;
  groupQty: Record<string, number | null>;
  valueOf: (row: DailyAllocationRow) => number;
  onGroupChange: (groupKey: string, rows: DailyAllocationRow[], value: number | null) => void;
  onQuantityChange: (row: DailyAllocationRow, value: number | null) => void;
};

/** Bấm vào ô nhập ở dòng nhóm không làm mở/đóng nhóm. */
const stopToggle = (e: MouseEvent) => e.stopPropagation();

/** Cột khung giao việc một công nhân: dòng nhóm (bấm để mở/đóng) có ô điền cả nhóm, dòng công đoạn có ô sửa riêng. */
export const dailyModalColumns = ({
  editable,
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
        <Space size={8} aria-expanded={r.isOpen}>
          {r.isOpen ? <DownOutlined /> : <RightOutlined />}
          <Typography.Text strong>
            {fill(PM_LABELS.groupRow, r.name ?? PM_LABELS.noGroup, r.rows.length)}
          </Typography.Text>
          {r.changedCount > 0 && (
            <Tag color="blue">{fill(PM_LABELS.groupChanged, r.changedCount)}</Tag>
          )}
        </Space>
      ) : (
        <>
          {multiLine && `${r.row.color}-${r.row.sizeCode} · `}
          {`${r.row.operationCode} - ${r.row.operationName}`}{' '}
          {!r.row.isAssigned && <Tag color="purple">{PM_LABELS.outsideAssignment}</Tag>}
        </>
      ),
  },
  {
    title: PM_LABELS.unitRate,
    key: 'unitRate',
    width: 110,
    align: 'right',
    render: (_, r) =>
      r.kind === 'group' ? null : r.row.unitRate == null ? (
        <Typography.Text type="secondary">{PM_LABELS.noRate}</Typography.Text>
      ) : (
        formatCurrency(r.row.unitRate)
      ),
  },
  {
    title: PM_LABELS.carried,
    key: 'carried',
    width: 120,
    align: 'right',
    render: (_, r) => (r.kind === 'group' ? null : r.row.carriedQty),
  },
  {
    title: PM_LABELS.allocated,
    key: 'qty',
    width: 150,
    render: (_, r) =>
      r.kind === 'group' ? (
        <span onClick={stopToggle}>
          <QtyInput
            aria-label={fill(PM_LABELS.groupInput, r.name ?? PM_LABELS.noGroup)}
            placeholder={PM_LABELS.groupPlaceholder}
            min={0}
            disabled={!editable}
            style={{ width: 120 }}
            value={groupQty[r.groupKey] ?? null}
            onChange={(v) => onGroupChange(r.groupKey, r.rows, v)}
          />
        </span>
      ) : (
        <QtyInput
          aria-label={fill(PM_LABELS.operationInput, r.row.operationCode)}
          disabled={!editable}
          min={minGiveOf(r.row)}
          max={maxGiveOf(r.row)}
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
