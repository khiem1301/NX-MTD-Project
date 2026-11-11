import { Descriptions, Progress, type TableColumnsType } from 'antd';
import { DataTable } from '@/components/common/DataTable';
import { DetailDescriptions, DetailModal } from '@/components/common/DetailModal';
import { formatQty } from '@/utils/format';
import { formatDeliveryWindow } from '@/utils/expectedDelivery';
import { INCOMING_TEXT, PERCENT_MAX } from '../constants';
import type { IncomingCustomerMaterial } from '../types';
import type { OrderGroup } from '../utils/groupByOrder';

const text = INCOMING_TEXT;

const lineColumns: TableColumnsType<IncomingCustomerMaterial> = [
  { title: text.materialCode, dataIndex: 'materialCode' },
  { title: text.materialName, dataIndex: 'materialName' },
  { title: text.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
  {
    title: text.customerAgreed,
    key: 'customerQty',
    align: 'right',
    render: (_, l) => `${formatQty(l.customerQty)} ${l.unit}`,
  },
  {
    title: text.receivedProgress,
    key: 'progress',
    width: 160,
    render: (_, l) => (
      <Progress
        size="small"
        percent={l.customerQty > 0 ? Math.round((l.receivedQty / l.customerQty) * PERCENT_MAX) : 0}
        format={() => formatQty(l.receivedQty)}
      />
    ),
  },
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

type IncomingCustomerDetailModalProps = {
  order: OrderGroup<IncomingCustomerMaterial> | null;
  onClose: () => void;
};

/** Xem chi tiết NVL khách cấp đang chờ nhận của một đơn hàng (chỉ đọc), dữ liệu lấy từ danh sách chờ nhận. */
export function IncomingCustomerDetailModal({ order, onClose }: IncomingCustomerDetailModalProps) {
  return (
    <DetailModal open={order !== null} onClose={onClose} title={order?.orderCode}>
      {order && (
        <>
          <DetailDescriptions>
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
            rowKey="orderMaterialId"
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
