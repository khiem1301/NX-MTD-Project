import { useState } from 'react';
import { Alert, Card, Typography } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { ReceiptFormModal } from '../components/ReceiptFormModal';
import {
  closedShortColumns,
  customerColumns,
  supplierColumns,
} from '../components/incomingMaterialColumns';
import { INCOMING_TEXT } from '../constants';
import { useIncomingMaterials } from '../hooks/useIncomingMaterials';
import type { ReceiptPreset } from '../types';
import { groupByPurchaseOrder } from '../utils/groupByPurchaseOrder';
import { PageCard } from '@/components/common/PageCard';
import { DataTable } from '@/components/common/DataTable';

const text = INCOMING_TEXT;

/**
 * Màn NVL chờ nhận của Thủ kho: NVL khách sẽ cấp (Quản đốc khai báo số lượng và số lần giao dự kiến ở "NVL của đơn") và đơn mua đã đặt
 * hàng với nhà cung cấp mà kho chưa nhận đủ. Hàng về thì bấm Lập phiếu nhập: nguồn, đơn và số còn phải nhận được điền sẵn.
 */
export function IncomingMaterialsPage() {
  const incoming = useIncomingMaterials();
  const [preset, setPreset] = useState<ReceiptPreset | null>(null);

  const data = incoming.data;
  return (
    <PageCard title={text.title} description={text.subtitle}>
      <QueryError error={incoming.error} onRetry={() => void incoming.refetch()} />
      <Card title={text.customerSection} size="small">
        <DataTable
          rowKey="orderMaterialId"
          columns={customerColumns(setPreset)}
          dataSource={data?.customer}
          loading={incoming.isFetching}
          pagination={false}
          locale={{ emptyText: text.customerEmpty }}
        />
      </Card>
      <Card title={text.supplierSection} size="small">
        <DataTable
          rowKey="purchaseOrderId"
          columns={supplierColumns(setPreset)}
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
          columns={closedShortColumns()}
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
