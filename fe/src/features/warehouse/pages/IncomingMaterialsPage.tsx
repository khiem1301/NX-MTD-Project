import { useState } from 'react';
import { Alert, Button, Card, Space, Table, Tag, Typography, type TableColumnsType } from 'antd';
import dayjs from 'dayjs';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { formatDate, formatQty } from '@/utils/format';
import { ReceiptFormModal } from '../components/ReceiptFormModal';
import { INCOMING_TEXT, MATERIAL_SOURCE } from '../constants';
import { useIncomingMaterials } from '../hooks/useIncomingMaterials';
import type { IncomingCustomerMaterial, IncomingSupplierMaterial, ReceiptPreset } from '../types';

const text = INCOMING_TEXT;

const dateCell = (value: string | null) => {
  if (!value) return <Typography.Text type="secondary">{text.noDate}</Typography.Text>;
  const late = dayjs(value).isBefore(dayjs(), 'day');
  return (
    <Space size={4}>
      {formatDate(value)}
      {late && <Tag color="red">{text.overdue}</Tag>}
    </Space>
  );
};

/**
 * Màn NVL chờ nhận của Thủ kho: NVL khách sẽ cấp (Quản đốc khai báo số lượng và ngày hẹn ở "NVL của đơn") và đơn mua đã đặt
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
    { title: text.expected, dataIndex: 'expectedDate', render: dateCell },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, r) => (
        <Button
          type="primary"
          size="small"
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
    { title: text.order, dataIndex: 'orderCode', render: (v: string | null) => v ?? text.stockPurchase },
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
    { title: text.expected, dataIndex: 'expectedDeliveryDate', render: dateCell },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, r) => (
        <Button
          type="primary"
          size="small"
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
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>
          {text.title}
        </Typography.Title>
        <Typography.Text type="secondary">{text.subtitle}</Typography.Text>
      </div>
      <QueryError error={incoming.error} onRetry={() => void incoming.refetch()} />
      <Card title={text.customerSection} size="small">
        <Table
          rowKey="orderMaterialId"
          size="small"
          columns={customerColumns}
          dataSource={data?.customer}
          loading={incoming.isFetching}
          pagination={false}
          locale={{ emptyText: text.customerEmpty }}
          scroll={{ x: 'max-content' }}
        />
      </Card>
      <Card title={text.supplierSection} size="small">
        <Table
          rowKey="purchaseOrderLineId"
          size="small"
          columns={supplierColumns}
          dataSource={data?.supplier}
          loading={incoming.isFetching}
          pagination={false}
          locale={{ emptyText: text.supplierEmpty }}
          scroll={{ x: 'max-content' }}
        />
      </Card>
      {data && data.customer.length === 0 && data.supplier.length === 0 && (
        <Alert type="info" showIcon message={text.customerEmpty + ' ' + text.supplierEmpty} />
      )}
      <ReceiptFormModal open={preset !== null} receipt={null} preset={preset} onClose={() => setPreset(null)} />
    </Space>
  );
}
