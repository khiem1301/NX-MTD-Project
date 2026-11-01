import { DeleteOutlined } from '@ant-design/icons';
import { InputNumber, Space, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { ColorAutoComplete } from './ColorAutoComplete';
import { rowTotal, type GridRow, type ProductBlock } from '../utils/orderGrid';
import { colorErrorKey, qtyErrorKey, type BlockErrors } from '../utils/orderValidation';

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
  errors: BlockErrors;
  setRow: (index: number, patch: Partial<GridRow>) => void;
  setQty: (index: number, size: string, qty: number | null) => void;
  removeSize: (size: string) => void;
  onChange: (block: ProductBlock) => void;
};

/** Cột lưới màu (hàng) × size (cột) của một khối mã hàng: màu, số lượng từng size, tổng, nút xóa dòng. */
export const blockGridColumns = ({
  block,
  locked,
  errors,
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
        size="small"
        status={errors[colorErrorKey(row.key)] ? 'error' : undefined}
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
          <IconAction
            title={`Xóa size ${size}`}
            icon={<DeleteOutlined />}
            onClick={() => removeSize(size)}
          />
        )}
      </Space>
    ),
    width: 90,
    render: (_: unknown, row: GridRow, index: number) => {
      const cell = row.cells[size];
      return (
        <InputNumber
          size="small"
          status={errors[qtyErrorKey(row.key, size)] ? 'error' : undefined}
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
    render: (_: unknown, row) => formatQty(rowTotal(block, row)),
  },
  {
    title: '',
    width: 40,
    render: (_: unknown, row, index) =>
      locked && rowHasOldLine(row) ? null : (
        <IconAction
          danger
          title={MESSAGES.common.delete}
          icon={<DeleteOutlined />}
          onClick={() => onChange({ ...block, rows: block.rows.filter((_r, i) => i !== index) })}
        />
      ),
  },
];
