import { InputNumber, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { fill, formatCurrency } from '@/utils/format';
import { OUTPUT_MAX_QTY, PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow } from '../types';
import { canFill } from '../utils/outputWorkerBoard';
import { OutputRowTags } from './OutputRowTags';

const text = PRODUCTION_TEXT.output;
const t = text.byWorker;

export type WorkerOutputTableRow =
  | { kind: 'group'; key: string; groupKey: string; name: string | null; rows: OutputGridRow[] }
  | { kind: 'operation'; key: string; row: OutputGridRow };

type WorkerOutputColumnsOptions = {
  groupQty: Record<string, number | null>;
  overRows: OutputGridRow[];
  valueOf: (row: OutputGridRow) => number | null;
  onGroupChange: (groupKey: string, rows: OutputGridRow[], value: number | null) => void;
  onQuantityChange: (operationId: number, value: number | null) => void;
};

/** Cột của khung nhập sản lượng một công nhân: dòng nhóm có ô điền cả nhóm, dòng công đoạn có ô sửa riêng. */
export const workerOutputColumns = ({
  groupQty,
  overRows,
  valueOf,
  onGroupChange,
  onQuantityChange,
}: WorkerOutputColumnsOptions): ColumnsType<WorkerOutputTableRow> => [
  {
    title: text.columns.operation,
    key: 'operation',
    ellipsis: true,
    render: (_, r) =>
      r.kind === 'group' ? (
        <Typography.Text strong>
          {fill(t.groupRow, r.name ?? t.noGroup, r.rows.filter(canFill).length, r.rows.length)}
        </Typography.Text>
      ) : (
        `${r.row.operationCode} - ${r.row.operationName}`
      ),
  },
  {
    title: text.columns.qty,
    key: 'qty',
    width: 150,
    render: (_, r) =>
      r.kind === 'group' ? (
        <InputNumber
          aria-label={fill(t.groupInput, r.name ?? t.noGroup)}
          placeholder={t.groupPlaceholder}
          disabled={!r.rows.some(canFill)}
          min={0}
          max={OUTPUT_MAX_QTY}
          precision={0}
          style={{ width: 120 }}
          value={groupQty[r.groupKey] ?? null}
          onChange={(v) => onGroupChange(r.groupKey, r.rows, v)}
        />
      ) : (
        <InputNumber
          aria-label={fill(t.operationInput, r.row.operationCode)}
          min={0}
          max={OUTPUT_MAX_QTY}
          precision={0}
          style={{ width: 120 }}
          status={overRows.includes(r.row) ? 'error' : undefined}
          disabled={!r.row.canEdit || !r.row.rateApproved}
          value={valueOf(r.row)}
          onChange={(v) => onQuantityChange(r.row.operationId, v)}
        />
      ),
  },
  {
    title: text.columns.allocated,
    key: 'allocated',
    width: 130,
    align: 'right',
    render: (_, r) =>
      r.kind === 'group' ? null : r.row.capQty > 0 ? (
        `${r.row.reportedQty} / ${r.row.capQty}`
      ) : (
        <Tag color="red" style={{ marginInlineEnd: 0 }}>
          {text.tags.notAllocated}
        </Tag>
      ),
  },
  {
    responsive: ['md'],
    title: text.columns.rate,
    key: 'rate',
    width: 110,
    align: 'right',
    render: (_, r) =>
      r.kind === 'group' || r.row.unitRate === null ? null : formatCurrency(r.row.unitRate),
  },
  {
    title: text.columns.warnings,
    key: 'warnings',
    width: 190,
    render: (_, r) => (r.kind === 'group' ? null : <OutputRowTags row={r.row} />),
  },
];
