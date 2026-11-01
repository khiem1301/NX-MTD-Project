import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Card, Flex, Form, Select, Space, Tooltip, Typography } from 'antd';
import { useState } from 'react';
import { SIZE_ORDER } from '@/constants/orderStatus';
import { MESSAGES } from '@/constants/messages';
import { useProductLookup } from '@/hooks/useOrderLookups';
import { formatQty } from '@/utils/format';
import { blockTotal, sortSizes, type GridRow, type ProductBlock } from '../utils/orderGrid';
import { DataTable } from '@/components/common/DataTable';
import { IconAction } from '@/components/common/IconAction';
import type { BlockErrors } from '../utils/orderValidation';
import { blockGridColumns, rowHasOldLine } from './blockGridColumns';
import { BlockSummaryRow } from './BlockSummaryRow';
import { CmtPriceInput } from './CmtPriceInput';

type ProductBlockEditorProps = {
  block: ProductBlock;
  /** Đơn không còn ở trạng thái Mới: dòng cũ không xóa được, không đổi mã hàng / màu / size (D27). */
  locked: boolean;
  /** Nhãn mã hàng đã biết (kể cả mã đã ngừng dùng, không có trong tra cứu). */
  productLabels: Record<number, string>;
  /** Mã hàng đã chọn ở các khối khác của đơn: không chọn lại (một mã hàng chỉ khai một lần trong đơn). */
  usedProductIds?: readonly number[];
  /** Lỗi của khối sau khi bấm Lưu (xem orderValidation). */
  errors?: BlockErrors;
  onChange: (block: ProductBlock) => void;
  onRemove: () => void;
  onCreateProduct: () => void;
};

const SIZE_OPTIONS = SIZE_ORDER.map((s) => ({ value: s, label: s }));
const NO_ERRORS: BlockErrors = {};
const FIELD_STYLE = { marginBottom: 0 };
const errorProps = (message: string | undefined) =>
  message ? { validateStatus: 'error' as const, help: message } : {};

/** Một khối mã hàng: chọn mã hàng + đơn giá CMT + lưới màu (hàng) × size (cột). */
export function ProductBlockEditor({
  block,
  locked,
  productLabels,
  usedProductIds = [],
  errors = NO_ERRORS,
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

  const addRow = () =>
    onChange({
      ...block,
      rows: [
        ...block.rows,
        { key: `row-${block.rows.length}-${Date.now()}`, color: '', cells: {} },
      ],
    });

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

  const columns = blockGridColumns({ block, locked, errors, setRow, setQty, removeSize, onChange });
  const gridErrors = [
    ...new Set(
      Object.entries(errors)
        .filter(([key]) => key !== 'product' && key !== 'price')
        .map(([, error]) => error),
    ),
  ];

  return (
    <Card size="small" style={{ marginBottom: 12 }}>
      <Form component={false} layout="vertical">
        <Flex wrap gap={12} align="flex-start" style={{ marginBottom: 12 }}>
          <Form.Item
            required
            label={MESSAGES.orders.productCode}
            style={{ ...FIELD_STYLE, width: 300 }}
            {...errorProps(errors.product)}
          >
            <Space.Compact block>
              <Tooltip title={lockKey ? MESSAGES.requests.otherFlowHint : undefined}>
                <Select
                  showSearch
                  filterOption={false}
                  placeholder={MESSAGES.orders.selectProduct}
                  style={{ width: '100%' }}
                  value={block.productId ?? undefined}
                  disabled={lockKey}
                  options={[...options].map(([value, label]) => ({ value, label }))}
                  onSearch={setKeyword}
                  onChange={(productId: number) => onChange({ ...block, productId })}
                />
              </Tooltip>
              <Tooltip title={MESSAGES.orders.createProduct}>
                <Button
                  icon={<PlusOutlined />}
                  aria-label={MESSAGES.orders.createProduct}
                  disabled={lockKey}
                  onClick={onCreateProduct}
                />
              </Tooltip>
            </Space.Compact>
          </Form.Item>
          <Form.Item
            required
            label={MESSAGES.orders.unitPrice}
            style={{ ...FIELD_STYLE, width: 180 }}
            {...errorProps(errors.price)}
          >
            <CmtPriceInput
              value={block.cmtUnitPrice}
              onChange={(cmtUnitPrice) => onChange({ ...block, cmtUnitPrice })}
            />
          </Form.Item>
          <Form.Item label=" " colon={false} style={{ ...FIELD_STYLE, marginLeft: 'auto' }}>
            <Space wrap>
              <Button icon={<PlusOutlined />} onClick={addRow}>
                {MESSAGES.orders.addColor}
              </Button>
              <Select
                showSearch
                placeholder={MESSAGES.orders.addSize}
                style={{ width: 120 }}
                value={null}
                options={SIZE_OPTIONS.filter((o) => !block.sizes.includes(o.value))}
                onSearch={() => undefined}
                onChange={(v: string) => addSize(v)}
                notFoundContent={null}
              />
              <Typography.Text
                strong
              >{`${MESSAGES.orders.totalQty}: ${formatQty(blockTotal(block))}`}</Typography.Text>
              {!lockKey && (
                <IconAction
                  danger
                  title={MESSAGES.orders.removeProductBlock}
                  icon={<DeleteOutlined />}
                  onClick={onRemove}
                />
              )}
            </Space>
          </Form.Item>
        </Flex>
      </Form>
      <DataTable<GridRow>
        rowKey="key"
        size="small"
        pagination={false}
        columns={columns}
        dataSource={block.rows}
        scroll={{ x: 'max-content' }}
        summary={() =>
          block.rows.length > 1 ? <BlockSummaryRow block={block} hasActionColumn /> : undefined
        }
      />
      {gridErrors.map((error) => (
        <div key={error} style={{ marginTop: 4 }}>
          <Typography.Text type="danger">{error}</Typography.Text>
        </div>
      ))}
    </Card>
  );
}
