import { useState } from 'react';
import { DataTable } from '@/components/common/DataTable';
import { INCOMING_TEXT } from '../constants';
import type { IncomingSupplierMaterial, ReceiptPreset } from '../types';
import type { PurchaseOrderGroup } from '../utils/groupByPurchaseOrder';
import { IncomingPurchaseDetailModal } from './IncomingPurchaseDetailModal';
import { supplierColumns } from './incomingMaterialColumns';

type SupplierIncomingTableProps = {
  rows: PurchaseOrderGroup<IncomingSupplierMaterial>[];
  loading: boolean;
  emptyText: string | undefined;
  onReceive: (preset: ReceiptPreset) => void;
};

/** Bảng đơn mua chờ nhận: mỗi đơn một dòng, nút Xem mở Modal chi tiết các NVL của đơn (như các màn khác). */
export function SupplierIncomingTable({
  rows,
  loading,
  emptyText,
  onReceive,
}: SupplierIncomingTableProps) {
  const [viewingId, setViewingId] = useState<number | null>(null);
  const viewing = rows.find((r) => r.purchaseOrderId === viewingId) ?? null;

  return (
    <>
      <DataTable
        rowKey="purchaseOrderId"
        columns={supplierColumns(onReceive, setViewingId)}
        dataSource={rows}
        loading={loading}
        locale={{ emptyText: emptyText ?? INCOMING_TEXT.supplierEmpty }}
      />
      <IncomingPurchaseDetailModal order={viewing} onClose={() => setViewingId(null)} />
    </>
  );
}
