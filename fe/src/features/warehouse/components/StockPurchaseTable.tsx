import { Table, Tag, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime, formatQty } from '@/utils/format';
import { LABELS, PO_GOODS_STATUS_LABEL, PO_PAYMENT_STATUS_LABEL } from '../constants';
import { usePurchaseOrder, usePurchaseOrders } from '../hooks/usePurchaseOrders';
import type { PurchaseOrderLine, PurchaseOrderListItem } from '../types';

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

const columns: TableColumnsType<PurchaseOrderListItem> = [
  { title: LABELS.poCode, dataIndex: 'poCode' },
  {
    title: LABELS.purchaseOrderOrder,
    dataIndex: 'orderCode',
    render: (v: string | null) => v ?? <Tag>{LABELS.stockPurchaseTag}</Tag>,
  },
  { title: LABELS.supplier, dataIndex: 'supplierName', render: (v: string | null) => v ?? '—' },
  { title: LABELS.lineCount, dataIndex: 'lineCount', align: 'right' },
  { title: LABELS.createdAt, dataIndex: 'createdAt', render: formatDateTime },
  {
    title: LABELS.goodsStatus,
    dataIndex: 'goodsStatus',
    render: (v: string) => <StatusTag status={v} labels={PO_GOODS_STATUS_LABEL} />,
  },
  {
    title: LABELS.paymentStatus,
    dataIndex: 'paymentStatus',
    render: (v: string) => <StatusTag status={v} labels={PO_PAYMENT_STATUS_LABEL} />,
  },
];

function PurchaseOrderLines({ id }: { id: number }) {
  const detail = usePurchaseOrder(id);
  return (
    <Table
      rowKey="purchaseOrderLineId"
      size="small"
      columns={lineColumns}
      dataSource={detail.data?.lines}
      loading={detail.isFetching}
      pagination={false}
    />
  );
}

/** Danh sách đơn mua NVL. stockOnly = true: chỉ đơn dùng chung (kim, chỉ...); undefined: cả đơn gắn đơn hàng. */
export function StockPurchaseTable({ stockOnly = true, goodsStatus }: { stockOnly?: boolean; goodsStatus?: string }) {
  const table = useTableQuery<Record<string, never>>({});
  const orders = usePurchaseOrders({ ...table.params, stockOnly, goodsStatus });
  return (
    <>
      <QueryError error={orders.error} onRetry={() => void orders.refetch()} />
      <Table
        rowKey="purchaseOrderId"
        columns={columns}
        dataSource={orders.data?.items}
        loading={orders.isFetching}
        pagination={table.pagination(orders.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
        expandable={{
          rowExpandable: (po) => po.lineCount > 0,
          expandedRowRender: (po) => <PurchaseOrderLines id={po.purchaseOrderId} />,
        }}
      />
    </>
  );
}
