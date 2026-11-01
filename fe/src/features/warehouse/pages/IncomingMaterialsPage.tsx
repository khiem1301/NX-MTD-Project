import { FileAddOutlined } from '@ant-design/icons';
import { useState, type ReactNode } from 'react';
import { Alert, Card, Space, Tag, Typography, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { ReceiptFormModal } from '../components/ReceiptFormModal';
import { INCOMING_TEXT, MATERIAL_SOURCE, PO_GOODS_STATUS_LABEL } from '../constants';
import { useIncomingMaterials } from '../hooks/useIncomingMaterials';
import type {
  ClosedShortPurchase,
  IncomingCustomerMaterial,
  IncomingSupplierMaterial,
  ReceiptPreset,
} from '../types';
import { groupByPurchaseOrder, type PurchaseOrderGroup } from '../utils/groupByPurchaseOrder';
import { StatusTag } from '@/components/common/StatusTag';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';

const text = INCOMING_TEXT;

/** Các dòng NVL của một đơn mua xếp chồng trong một ô, các cột số lượng thẳng hàng với cột NVL. */
const Stack = ({ children }: { children: ReactNode }) => (
  <Space direction="vertical" size={4} style={{ width: '100%' }}>
    {children}
  </Space>
);

/** "đã nhận / dự kiến" lần giao; đã nhận đủ số lần mà vẫn còn thiếu thì báo "Đủ lần giao" để Thủ kho biết hệ thống sẽ lập đơn thiếu NVL. */
const deliveryCell = (expected: number | null, received: number) => {
  if (expected === null)
    return <Typography.Text type="secondary">{text.noDeliveryCount}</Typography.Text>;
  return (
    <Space size={4}>
      {received} / {expected} {text.deliveryUnit}
      {received >= expected && <Tag color="orange">{text.deliveriesDone}</Tag>}
    </Space>
  );
};

/**
 * Màn NVL chờ nhận của Thủ kho: NVL khách sẽ cấp (Quản đốc khai báo số lượng và số lần giao dự kiến ở "NVL của đơn") và đơn mua đã đặt
 * hàng với nhà cung cấp mà kho chưa nhận đủ. Hàng về thì bấm Lập phiếu nhập: nguồn, đơn và số còn phải nhận được điền sẵn.
 */
export function IncomingMaterialsPage() {
  const incoming = useIncomingMaterials();
  const [preset, setPreset] = useState<ReceiptPreset | null>(null);

  const customerColumns: TableColumnsType<IncomingCustomerMaterial> = [
    { title: text.order, dataIndex: 'orderCode' },
    {
      title: text.material,
      key: 'material',
      render: (_, r) => (
        <>
          <b>{r.materialCode}</b> {r.materialName}
        </>
      ),
    },
    { title: text.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
    { title: text.customerAgreed, dataIndex: 'customerQty', align: 'right', render: formatQty },
    { title: text.received, dataIndex: 'receivedQty', align: 'right', render: formatQty },
    {
      title: text.outstanding,
      dataIndex: 'outstandingQty',
      align: 'right',
      render: (v: number, r) => (
        <b>
          {formatQty(v)} {r.unit}
        </b>
      ),
    },
    {
      title: text.deliveries,
      key: 'deliveries',
      render: (_, r) => deliveryCell(r.expectedDeliveryCount, r.receivedCount),
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, r) => (
        <IconAction
          title={text.createReceipt}
          icon={<FileAddOutlined />}
          onClick={() =>
            setPreset({
              source: MATERIAL_SOURCE.CUSTOMER,
              orderId: r.orderId,
              orderMaterialId: r.orderMaterialId,
              acceptedQty: r.outstandingQty,
            })
          }
        />
      ),
    },
  ];

  const supplierColumns: TableColumnsType<PurchaseOrderGroup<IncomingSupplierMaterial>> = [
    { title: text.purchaseOrder, dataIndex: 'poCode' },
    {
      title: text.order,
      dataIndex: 'orderCode',
      render: (v: string | null) => v ?? text.stockPurchase,
    },
    { title: text.supplier, dataIndex: 'supplierName', render: (v: string | null) => v ?? '—' },
    {
      title: text.material,
      key: 'material',
      render: (_, r) => (
        <Stack>
          {r.lines.map((l) => (
            <div key={l.purchaseOrderLineId}>
              <b>{l.materialCode}</b> {l.materialName}
              {l.materialColor ? ` - ${l.materialColor}` : ''}
            </div>
          ))}
        </Stack>
      ),
    },
    {
      title: text.ordered,
      key: 'ordered',
      align: 'right',
      render: (_, r) => (
        <Stack>
          {r.lines.map((l) => (
            <div key={l.purchaseOrderLineId}>{formatQty(l.orderedQty)}</div>
          ))}
        </Stack>
      ),
    },
    {
      title: text.received,
      key: 'received',
      align: 'right',
      render: (_, r) => (
        <Stack>
          {r.lines.map((l) => (
            <div key={l.purchaseOrderLineId}>{formatQty(l.receivedQty)}</div>
          ))}
        </Stack>
      ),
    },
    {
      title: text.outstanding,
      key: 'outstanding',
      align: 'right',
      render: (_, r) => (
        <Stack>
          {r.lines.map((l) => (
            <div key={l.purchaseOrderLineId}>
              <b>
                {formatQty(l.outstandingQty)} {l.unit}
              </b>
            </div>
          ))}
        </Stack>
      ),
    },
    {
      title: text.deliveries,
      key: 'deliveries',
      render: (_, r) => deliveryCell(r.expectedDeliveryCount, r.receivedCount),
    },
    {
      title: text.goodsStatus,
      dataIndex: 'goodsStatus',
      render: (v: string) => (
        <Space direction="vertical" size={0}>
          <StatusTag status={v} labels={PO_GOODS_STATUS_LABEL} />
          <Typography.Text type="success">{text.canReceiveMore}</Typography.Text>
        </Space>
      ),
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, r) => (
        <IconAction
          title={text.createReceipt}
          icon={<FileAddOutlined />}
          onClick={() =>
            setPreset({
              source: MATERIAL_SOURCE.SUPPLIER,
              purchaseOrderId: r.purchaseOrderId,
              acceptedQty: 0,
              purchaseLines: r.lines.map((l) => ({
                purchaseOrderLineId: l.purchaseOrderLineId,
                acceptedQty: l.outstandingQty,
              })),
            })
          }
        />
      ),
    },
  ];

  const closedShortColumns: TableColumnsType<PurchaseOrderGroup<ClosedShortPurchase>> = [
    { title: text.purchaseOrder, dataIndex: 'poCode' },
    {
      title: text.order,
      dataIndex: 'orderCode',
      render: (v: string | null) => v ?? text.stockPurchase,
    },
    { title: text.supplier, dataIndex: 'supplierName', render: (v: string | null) => v ?? '—' },
    {
      title: text.material,
      key: 'material',
      render: (_, r) => (
        <Stack>
          {r.lines.map((l) => (
            <div key={l.purchaseOrderLineId}>
              <b>{l.materialCode}</b> {l.materialName}
              {l.materialColor ? ` - ${l.materialColor}` : ''}
            </div>
          ))}
        </Stack>
      ),
    },
    {
      title: text.ordered,
      key: 'ordered',
      align: 'right',
      render: (_, r) => (
        <Stack>
          {r.lines.map((l) => (
            <div key={l.purchaseOrderLineId}>{formatQty(l.orderedQty)}</div>
          ))}
        </Stack>
      ),
    },
    {
      title: text.received,
      key: 'received',
      align: 'right',
      render: (_, r) => (
        <Stack>
          {r.lines.map((l) => (
            <div key={l.purchaseOrderLineId}>{formatQty(l.receivedQty)}</div>
          ))}
        </Stack>
      ),
    },
    {
      title: text.notReplenished,
      key: 'notReplenished',
      align: 'right',
      render: (_, r) => (
        <Stack>
          {r.lines.map((l) => (
            <div key={l.purchaseOrderLineId}>
              <b>
                {formatQty(l.notReplenishedQty)} {l.unit}
              </b>
            </div>
          ))}
        </Stack>
      ),
    },
    {
      title: text.goodsStatus,
      key: 'status',
      render: () => <StatusTag status="CLOSED_SHORT" labels={PO_GOODS_STATUS_LABEL} />,
    },
  ];

  const data = incoming.data;
  return (
    <PageCard title={text.title} description={text.subtitle}>
      <QueryError error={incoming.error} onRetry={() => void incoming.refetch()} />
      <Card title={text.customerSection} size="small">
        <DataTable
          rowKey="orderMaterialId"
          columns={customerColumns}
          dataSource={data?.customer}
          loading={incoming.isFetching}
          pagination={false}
          locale={{ emptyText: text.customerEmpty }}
        />
      </Card>
      <Card title={text.supplierSection} size="small">
        <DataTable
          rowKey="purchaseOrderId"
          columns={supplierColumns}
          dataSource={groupByPurchaseOrder(data?.supplier)}
          loading={incoming.isFetching}
          pagination={false}
          locale={{ emptyText: text.supplierEmpty }}
        />
      </Card>
      <Card title={text.closedShortSection} size="small">
        <Typography.Paragraph type="secondary">{text.closedShortHint}</Typography.Paragraph>
        <DataTable
          rowKey="purchaseOrderId"
          columns={closedShortColumns}
          dataSource={groupByPurchaseOrder(data?.closedShort)}
          loading={incoming.isFetching}
          pagination={false}
          locale={{ emptyText: text.closedShortEmpty }}
        />
      </Card>
      {data && data.customer.length === 0 && data.supplier.length === 0 && (
        <Alert type="info" showIcon message={text.customerEmpty + ' ' + text.supplierEmpty} />
      )}
      <ReceiptFormModal
        open={preset !== null}
        receipt={null}
        preset={preset}
        onClose={() => setPreset(null)}
      />
    </PageCard>
  );
}
