import { DeleteOutlined } from '@ant-design/icons';
import { InputNumber, Tag } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { IconAction } from '@/components/common/IconAction';
import { formatCurrency, formatDateTime } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import type { OutputGridRow } from '../types';
import { outputKeyOf } from '../utils';
import { OutputRowTags } from './OutputRowTags';

const text = PRODUCTION_TEXT.output;

type OutputColumnsOptions = {
  isManager: boolean;
  /** Số lượng đang sửa theo khóa dòng; chưa sửa thì hiện số đã lưu. */
  edits: Record<string, number | null>;
  isExtraRow: (row: OutputGridRow) => boolean;
  onQuantityChange: (row: OutputGridRow, value: number | null) => void;
  onRemoveExtra: (row: OutputGridRow) => void;
};

/** Cột lưới sản lượng cuối ca: nhập số lượng ngay trên dòng, xóa được dòng ngoài phân công vừa thêm. */
export const outputColumns = ({
  isManager,
  edits,
  isExtraRow,
  onQuantityChange,
  onRemoveExtra,
}: OutputColumnsOptions): ColumnsType<OutputGridRow> => [
  {
    title: text.columns.operation,
    key: 'op',
    render: (_, r) => `${r.operationCode} - ${r.operationName}`,
  },
  {
    title: text.columns.worker,
    key: 'worker',
    render: (_, r) => `${r.workerCode} - ${r.workerName}`,
  },
  {
    title: text.columns.qty,
    key: 'qty',
    width: 160,
    render: (_, r) => (
      <InputNumber
        min={0}
        precision={0}
        style={{ width: 120 }}
        disabled={!isManager && !r.canEdit}
        value={edits[outputKeyOf(r)] !== undefined ? edits[outputKeyOf(r)] : r.quantity}
        onChange={(v) => onQuantityChange(r, v)}
      />
    ),
  },
  {
    title: text.columns.allocated,
    key: 'allocated',
    align: 'right',
    render: (_, r) =>
      r.capQty > 0 ? (
        `${r.reportedQty} / ${r.capQty}`
      ) : (
        <Tag color="red">{text.tags.notAllocated}</Tag>
      ),
  },
  {
    responsive: ['md'],
    title: text.columns.rate,
    dataIndex: 'unitRate',
    align: 'right',
    render: (v: number | null) => (v === null ? '—' : formatCurrency(v)),
  },
  {
    responsive: ['md'],
    title: text.columns.deadline,
    dataIndex: 'editDeadline',
    render: (v: string | null) => formatDateTime(v),
  },
  {
    title: text.columns.warnings,
    key: 'warnings',
    render: (_, r) => <OutputRowTags row={r} />,
  },
  {
    title: '',
    key: 'remove',
    render: (_, r) =>
      r.outputRecordId === null && isExtraRow(r) ? (
        <IconAction
          title={text.removeRow}
          icon={<DeleteOutlined />}
          danger
          onClick={() => onRemoveExtra(r)}
        />
      ) : null,
  },
];
