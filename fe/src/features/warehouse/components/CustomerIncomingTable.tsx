import { useState } from 'react';
import { DataTable } from '@/components/common/DataTable';
import { INCOMING_TEXT } from '../constants';
import type { IncomingCustomerMaterial, ReceiptPreset } from '../types';
import type { OrderGroup } from '../utils/groupByOrder';
import { IncomingCustomerDetailModal } from './IncomingCustomerDetailModal';
import { customerColumns } from './incomingMaterialColumns';

type CustomerIncomingTableProps = {
  rows: OrderGroup<IncomingCustomerMaterial>[];
  loading: boolean;
  emptyText: string | undefined;
  onReceive: (preset: ReceiptPreset) => void;
};

/** Bảng NVL khách cấp chờ nhận: mỗi đơn hàng một dòng, nút Xem mở Modal chi tiết các NVL của đơn. */
export function CustomerIncomingTable({
  rows,
  loading,
  emptyText,
  onReceive,
}: CustomerIncomingTableProps) {
  const [viewingId, setViewingId] = useState<number | null>(null);
  const viewing = rows.find((r) => r.orderId === viewingId) ?? null;

  return (
    <>
      <DataTable
        rowKey="orderId"
        columns={customerColumns(onReceive, setViewingId)}
        dataSource={rows}
        loading={loading}
        locale={{ emptyText: emptyText ?? INCOMING_TEXT.customerEmpty }}
      />
      <IncomingCustomerDetailModal order={viewing} onClose={() => setViewingId(null)} />
    </>
  );
}
