import { useState } from 'react';
import { Alert, Button, Card, Space, Tag, Typography, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { ReceiptFormModal } from '../components/ReceiptFormModal';
import { INCOMING_TEXT, MATERIAL_SOURCE } from '../constants';
import { useIncomingMaterials } from '../hooks/useIncomingMaterials';
import type { IncomingCustomerMaterial, IncomingSupplierMaterial, ReceiptPreset } from '../types';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';

const text = INCOMING_TEXT;

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
        <Button
          type="link"
          onClick={() =>
            setPreset({
              source: MATERIAL_SOURCE.CUSTOMER,
              orderId: r.orderId,
              orderMaterialId: r.orderMaterialId,
              acceptedQty: r.outstandingQty,
            })
          }
        >
          {text.createReceipt}
        </Button>
      ),
    },
  ];

  const supplierColumns: TableColumnsType<IncomingSupplierMaterial> = [
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
        <>
          <b>{r.materialCode}</b> {r.materialName}
          {r.materialColor ? ` - ${r.materialColor}` : ''}
        </>
      ),
    },
    { title: text.ordered, dataIndex: 'orderedQty', align: 'right', render: formatQty },
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
        <Button
          type="link"
          onClick={() =>
            setPreset({
              source: MATERIAL_SOURCE.SUPPLIER,
              purchaseOrderId: r.purchaseOrderId,
              purchaseOrderLineId: r.purchaseOrderLineId,
              acceptedQty: r.outstandingQty,
            })
          }
        >
          {text.createReceipt}
        </Button>
      ),
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
          rowKey="purchaseOrderLineId"
          columns={supplierColumns}
          dataSource={data?.supplier}
          loading={incoming.isFetching}
          pagination={false}
          locale={{ emptyText: text.supplierEmpty }}
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
