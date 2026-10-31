import { DeleteOutlined } from '@ant-design/icons';
import { Button, InputNumber, Space, type TableColumnsType } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { ColorAutoComplete } from './ColorAutoComplete';
import type { GridRow, ProductBlock } from '../utils/orderGrid';

export const rowHasOldLine = (row: GridRow): boolean =>
  Object.values(row.cells).some((c) => c.orderLineId !== null);
const sizeHasOldLine = (block: ProductBlock, size: string): boolean =>
  block.rows.some(
    (r) => r.cells[size]?.orderLineId !== undefined && r.cells[size]?.orderLineId !== null,
  );

type BlockGridColumnsOptions = {
  block: ProductBlock;
  /** Đơn không còn ở trạng thái Mới: dòng / size cũ không xóa được, không đổi màu. */
  locked: boolean;
  setRow: (index: number, patch: Partial<GridRow>) => void;
  setQty: (index: number, size: string, qty: number | null) => void;
  removeSize: (size: string) => void;
  onChange: (block: ProductBlock) => void;
};

/** Cột lưới màu (hàng) × size (cột) của một khối mã hàng: màu, số lượng từng size, tổng, nút xóa dòng. */
export const blockGridColumns = ({
  block,
  locked,
  setRow,
  setQty,
  removeSize,
  onChange,
}: BlockGridColumnsOptions): TableColumnsType<GridRow> => [
  {
    title: MESSAGES.orders.color,
    width: 160,
    render: (_: unknown, row, index) => (
      <ColorAutoComplete
        value={row.color}
        disabled={locked && rowHasOldLine(row)}
        extraColors={block.rows.map((r) => r.color)}
        onChange={(color) => setRow(index, { color })}
      />
    ),
  },
  ...block.sizes.map((size) => ({
    title: (
      <Space size={4}>
        {size}
        {!(locked && sizeHasOldLine(block, size)) && (
          <Button
            type="text"
            size="small"
            icon={<DeleteOutlined />}
            aria-label={`Xóa size ${size}`}
            onClick={() => removeSize(size)}
          />
        )}
      </Space>
    ),
    width: 110,
    render: (_: unknown, row: GridRow, index: number) => {
      const cell = row.cells[size];
      const old = cell?.orderLineId !== undefined && cell.orderLineId !== null;
      return (
        <InputNumber
          min={locked && old ? 1 : 0}
          precision={0}
          controls={false}
          style={{ width: '100%' }}
          value={cell?.qty ?? null}
          onChange={(v) => setQty(index, size, v)}
        />
      );
    },
  })),
  {
    title: MESSAGES.orders.totalQty,
    width: 90,
    align: 'right',
    render: (_: unknown, row) =>
      formatQty(block.sizes.reduce((s, size) => s + (row.cells[size]?.qty ?? 0), 0)),
  },
  {
    title: '',
    width: 48,
    render: (_: unknown, row, index) =>
      locked && rowHasOldLine(row) ? null : (
        <Button
          type="text"
          danger
          icon={<DeleteOutlined />}
          aria-label={MESSAGES.common.delete}
          onClick={() => onChange({ ...block, rows: block.rows.filter((_r, i) => i !== index) })}
        />
      ),
  },
];
