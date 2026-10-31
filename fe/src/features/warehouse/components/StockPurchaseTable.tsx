import { useState } from 'react';
import { Tag, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import { LABELS, PO_GOODS_STATUS_LABEL, PO_PAYMENT_STATUS_LABEL } from '../constants';
import { usePurchaseOrders } from '../hooks/usePurchaseOrders';
import type { PurchaseOrderListItem } from '../types';
import { DetailModal } from '@/components/common/DetailModal';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';
import { PurchaseOrderLines } from './PurchaseOrderLines';

const columns = (
  onView: (po: PurchaseOrderListItem) => void,
): TableColumnsType<PurchaseOrderListItem> => [
  {
    title: LABELS.poCode,
    dataIndex: 'poCode',
    render: (v: string, po) => <DetailLink onClick={() => onView(po)}>{v}</DetailLink>,
  },
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

/** Danh sách đơn mua NVL. stockOnly = true: chỉ đơn dùng chung (kim, chỉ...); undefined: cả đơn gắn đơn hàng. */
export function StockPurchaseTable({
  stockOnly,
  goodsStatus,
}: {
  stockOnly?: boolean;
  goodsStatus?: string;
}) {
  const table = useTableQuery<Record<string, never>>({});
  const orders = usePurchaseOrders({ ...table.params, stockOnly, goodsStatus });
  const [viewing, setViewing] = useState<PurchaseOrderListItem | null>(null);
  return (
    <>
      <QueryError error={orders.error} onRetry={() => void orders.refetch()} />
      <DataTable
        rowKey="purchaseOrderId"
        columns={columns(setViewing)}
        dataSource={orders.data?.items}
        loading={orders.isFetching}
        pagination={table.pagination(orders.data?.totalItems)}
      />
      <DetailModal open={viewing !== null} onClose={() => setViewing(null)} title={viewing?.poCode}>
        {viewing && <PurchaseOrderLines id={viewing.purchaseOrderId} />}
      </DetailModal>
    </>
  );
}
