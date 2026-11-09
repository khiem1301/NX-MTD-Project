import { Form, Input, type TablePaginationConfig } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { QtyInput } from '@/components/common/QtyInput';
import { MAX_QTY, QTY_DECIMALS, type StockBalance } from '@/features/warehouse';
import { formatQty } from '@/utils/format';
import { ST_LABELS } from '../constants';
import { targetKey } from '../utils/stocktakeLines';

type StocktakeCountTableProps = {
  isMaterial: boolean;
  balances: StockBalance[] | undefined;
  loading: boolean;
  pagination: TablePaginationConfig;
};

const rowName = (b: StockBalance) =>
  b.materialId !== null
    ? `${b.materialCode} ${b.materialName}${b.materialColor ? ` - ${b.materialColor}` : ''}`
    : `${b.orderCode} · ${b.productCode} ${b.color}-${b.sizeCode}${b.componentName ? ` · ${b.componentName}` : ''}`;

/**
 * Bảng nhập số đếm của phiếu kiểm kê, nằm trong Form của phiếu (trường counted / notes theo từng hàng hóa). Số đếm
 * không âm, tối đa 100.000.000; kho NVL tối đa 3 số lẻ, kho BTP / thành phẩm đếm theo cái nên là số nguyên.
 */
export function StocktakeCountTable({
  isMaterial,
  balances,
  loading,
  pagination,
}: StocktakeCountTableProps) {
  return (
    <DataTable<StockBalance>
      rowKey={targetKey}
      size="small"
      loading={loading}
      dataSource={balances}
      pagination={pagination}
      scroll={{ y: 360 }}
      locale={{ emptyText: ST_LABELS.noRows }}
      columns={[
        { title: ST_LABELS.item, key: 'item', render: (_, b) => rowName(b) },
        { title: ST_LABELS.unit, dataIndex: 'unit', render: (v: string | null) => v ?? 'cái' },
        { title: ST_LABELS.bookQty, dataIndex: 'onHandQty', align: 'right', render: formatQty },
        {
          title: ST_LABELS.countedQty,
          key: 'counted',
          render: (_, b) => (
            <Form.Item name={['counted', targetKey(b)]} noStyle>
              <QtyInput
                max={MAX_QTY}
                decimals={isMaterial ? QTY_DECIMALS : 0}
                style={{ width: 140 }}
              />
            </Form.Item>
          ),
        },
        {
          title: ST_LABELS.note,
          key: 'note',
          render: (_, b) => (
            <Form.Item name={['notes', targetKey(b)]} noStyle>
              <Input maxLength={300} />
            </Form.Item>
          ),
        },
      ]}
    />
  );
}
