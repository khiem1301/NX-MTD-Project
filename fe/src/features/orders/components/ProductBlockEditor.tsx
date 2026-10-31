import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import {
  Button,
  Card,
  InputNumber,
  Select,
  Space,
  Tooltip,
  Typography,
  type TableColumnsType,
} from 'antd';
import { useState } from 'react';
import { SIZE_ORDER } from '@/constants/orderStatus';
import { MESSAGES } from '@/constants/messages';
import { useProductLookup } from '@/hooks/useOrderLookups';
import { formatQty } from '@/utils/format';
import { ColorAutoComplete } from './ColorAutoComplete';
import { blockTotal, sortSizes, type GridRow, type ProductBlock } from '../utils/orderGrid';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type ProductBlockEditorProps = {
  block: ProductBlock;
  /** Đơn không còn ở trạng thái Mới: dòng cũ không xóa được, không đổi mã hàng / màu / size (D27). */
  locked: boolean;
  /** Nhãn mã hàng đã biết (kể cả mã đã ngừng dùng, không có trong tra cứu). */
  productLabels: Record<number, string>;
  /** Mã hàng đã chọn ở các khối khác của đơn: không chọn lại (một mã hàng chỉ khai một lần trong đơn). */
  usedProductIds?: readonly number[];
  onChange: (block: ProductBlock) => void;
  onRemove: () => void;
  onCreateProduct: () => void;
};

const SIZE_OPTIONS = SIZE_ORDER.map((s) => ({ value: s, label: s }));
const rowHasOldLine = (row: GridRow): boolean =>
  Object.values(row.cells).some((c) => c.orderLineId !== null);
const sizeHasOldLine = (block: ProductBlock, size: string): boolean =>
  block.rows.some(
    (r) => r.cells[size]?.orderLineId !== undefined && r.cells[size]?.orderLineId !== null,
  );

/** Một khối mã hàng: chọn mã hàng + đơn giá CMT + lưới màu (hàng) × size (cột). */
export function ProductBlockEditor({
  block,
  locked,
  productLabels,
  usedProductIds = [],
  onChange,
  onRemove,
  onCreateProduct,
}: ProductBlockEditorProps) {
  const [keyword, setKeyword] = useState<string>();
  const products = useProductLookup(keyword);
  const hasOldLines = block.rows.some(rowHasOldLine);
  const lockKey = locked && hasOldLines;

  const options = new Map<number, string>();
  if (block.productId !== null && productLabels[block.productId])
    options.set(block.productId, productLabels[block.productId] as string);
  for (const p of products.data?.items ?? [])
    options.set(p.productId, `${p.productCode} · ${p.productName}`);
  for (const id of usedProductIds) if (id !== block.productId) options.delete(id);

  const setRow = (index: number, patch: Partial<GridRow>) =>
    onChange({ ...block, rows: block.rows.map((r, i) => (i === index ? { ...r, ...patch } : r)) });

  const setQty = (index: number, size: string, qty: number | null) => {
    const row = block.rows[index] as GridRow;
    const cell = row.cells[size] ?? { orderLineId: null, qty: null };
    setRow(index, { cells: { ...row.cells, [size]: { ...cell, qty } } });
  };

  const addSize = (size: string) => {
    const normalized = size.trim().toUpperCase();
    if (normalized && !block.sizes.includes(normalized))
      onChange({ ...block, sizes: sortSizes([...block.sizes, normalized]) });
  };

  const removeSize = (size: string) =>
    onChange({
      ...block,
      sizes: block.sizes.filter((s) => s !== size),
      rows: block.rows.map((r) => {
        const { [size]: _removed, ...cells } = r.cells;
        return { ...r, cells };
      }),
    });

  const columns: TableColumnsType<GridRow> = [
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

  return (
    <Card size="small" style={{ marginBottom: 16 }}>
      <TableToolbar
        filters={
          <>
            <Tooltip title={lockKey ? MESSAGES.requests.otherFlowHint : undefined}>
              <Select
                showSearch
                filterOption={false}
                placeholder={MESSAGES.orders.selectProduct}
                style={{ width: 280 }}
                value={block.productId ?? undefined}
                disabled={lockKey}
                options={[...options].map(([value, label]) => ({ value, label }))}
                onSearch={setKeyword}
                onChange={(productId: number) => onChange({ ...block, productId })}
              />
            </Tooltip>
            <Button icon={<PlusOutlined />} disabled={lockKey} onClick={onCreateProduct}>
              {MESSAGES.common.create}
            </Button>
            <InputNumber
              min={0}
              precision={2}
              addonAfter="₫"
              placeholder={MESSAGES.orders.unitPrice}
              style={{ width: 200 }}
              formatter={(v) => `${v ?? ''}`.replace(/\B(?=(\d{3})+(?!\d))/g, '.')}
              parser={(v) => Number((v ?? '').replace(/\./g, ''))}
              value={block.cmtUnitPrice}
              onChange={(v) => onChange({ ...block, cmtUnitPrice: v })}
            />
            <Typography.Text
              strong
            >{`${MESSAGES.orders.totalQty}: ${formatQty(blockTotal(block))}`}</Typography.Text>
            {!lockKey && (
              <Button danger onClick={onRemove}>
                {MESSAGES.common.delete}
              </Button>
            )}
          </>
        }
      />
      <DataTable<GridRow>
        rowKey="key"
        size="small"
        pagination={false}
        columns={columns}
        dataSource={block.rows}
        scroll={{ x: 'max-content' }}
      />
      <Space style={{ marginTop: 8 }} wrap>
        <Button
          icon={<PlusOutlined />}
          onClick={() =>
            onChange({
              ...block,
              rows: [
                ...block.rows,
                { key: `row-${block.rows.length}-${Date.now()}`, color: '', cells: {} },
              ],
            })
          }
        >
          {MESSAGES.orders.addColor}
        </Button>
        <Select
          showSearch
          placeholder={MESSAGES.orders.addSize}
          style={{ width: 130 }}
          value={null}
          options={SIZE_OPTIONS.filter((o) => !block.sizes.includes(o.value))}
          onSearch={() => undefined}
          onChange={(v: string) => addSize(v)}
          notFoundContent={null}
        />
      </Space>
    </Card>
  );
}
