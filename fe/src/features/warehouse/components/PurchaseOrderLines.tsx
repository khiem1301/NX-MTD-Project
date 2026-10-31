import { usePurchaseOrder } from '../hooks/usePurchaseOrders';
import { DataTable } from '@/components/common/DataTable';
import { type TableColumnsType } from 'antd';
import { formatQty } from '@/utils/format';
import { LABELS } from '../constants';
import type { PurchaseOrderLine } from '../types';

const lineColumns: TableColumnsType<PurchaseOrderLine> = [
  {
    title: LABELS.material,
    key: 'material',
    render: (_, l) => (
      <>
        <b>{l.materialCode}</b> {l.materialName}
      </>
    ),
  },
  { title: LABELS.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  { title: LABELS.unit, dataIndex: 'unit' },
  { title: LABELS.orderedQty, dataIndex: 'orderedQty', align: 'right', render: formatQty },
  { title: LABELS.unitPrice, dataIndex: 'unitPrice', align: 'right', render: formatQty },
  { title: LABELS.amount, dataIndex: 'amount', align: 'right', render: formatQty },
];

export function PurchaseOrderLines({ id }: { id: number }) {
  const detail = usePurchaseOrder(id);
  return (
    <DataTable
      rowKey="purchaseOrderLineId"
      columns={lineColumns}
      dataSource={detail.data?.lines}
      loading={detail.isFetching}
      pagination={false}
    />
  );
}
