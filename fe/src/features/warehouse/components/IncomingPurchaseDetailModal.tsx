import { Descriptions, type TableColumnsType } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DetailDescriptions, DetailModal } from '@/components/common/DetailModal';
import { StatusTag } from '@/components/common/StatusTag';
import { formatQty } from '@/utils/format';
import { formatDeliveryWindow } from '@/utils/expectedDelivery';
import { INCOMING_TEXT, PO_GOODS_STATUS_LABEL } from '../constants';
import type { IncomingSupplierMaterial } from '../types';
import type { PurchaseOrderGroup } from '../utils/groupByPurchaseOrder';

const text = INCOMING_TEXT;

const lineColumns: TableColumnsType<IncomingSupplierMaterial> = [
  { title: text.materialCode, dataIndex: 'materialCode' },
  { title: text.materialName, dataIndex: 'materialName' },
  { title: text.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  { title: text.ordered, dataIndex: 'orderedQty', align: 'right', render: formatQty },
  { title: text.received, dataIndex: 'receivedQty', align: 'right', render: formatQty },
  {
    title: text.outstanding,
    key: 'outstanding',
    align: 'right',
    render: (_, l) => (
      <b>
        {formatQty(l.outstandingQty)} {l.unit}
      </b>
    ),
  },
];

type IncomingPurchaseDetailModalProps = {
  order: PurchaseOrderGroup<IncomingSupplierMaterial> | null;
  onClose: () => void;
};

/** Xem chi tiết đơn mua đang chờ nhận (chỉ đọc): thông tin đơn và các dòng NVL, dữ liệu lấy từ danh sách chờ nhận. */
export function IncomingPurchaseDetailModal({ order, onClose }: IncomingPurchaseDetailModalProps) {
  return (
    <DetailModal open={order !== null} onClose={onClose} title={order?.poCode}>
      {order && (
        <>
          <DetailDescriptions>
            <Descriptions.Item label={text.order}>
              {order.orderCode ?? text.stockPurchase}
            </Descriptions.Item>
            <Descriptions.Item label={text.supplier}>{order.supplierName ?? '—'}</Descriptions.Item>
            <Descriptions.Item label={text.goodsStatus}>
              <StatusTag status={order.goodsStatus} labels={PO_GOODS_STATUS_LABEL} />
            </Descriptions.Item>
            <Descriptions.Item label={text.deliveries}>
              {order.expectedDeliveryCount === null
                ? text.noDeliveryCount
                : `${order.receivedCount} / ${order.expectedDeliveryCount} ${text.deliveryUnit}`}
            </Descriptions.Item>
            <Descriptions.Item label={text.expectedDeliveryDate}>
              {formatDeliveryWindow(order.expectedDeliveryDate, order.expectedDeliveryDateTo)}
            </Descriptions.Item>
          </DetailDescriptions>
          <DataTable
            rowKey="purchaseOrderLineId"
            style={{ marginTop: 16 }}
            columns={lineColumns}
            dataSource={order.lines}
            pagination={false}
          />
        </>
      )}
    </DetailModal>
  );
}
