import { useState } from 'react';
import { EditOutlined, PlusOutlined } from '@ant-design/icons';
import { useSearchParams } from 'react-router-dom';
import { Button } from 'antd';
import { PageCard } from '@/components/common/PageCard';
import { MESSAGES } from '@/constants/messages';
import { useOrderOptions } from '@/hooks/useLookups';
import { orderMaterialColumns } from '../components/orderMaterialColumns';
import { OrderMaterialBulkEditor } from '../components/OrderMaterialBulkEditor';
import { OrderMaterialFormModal } from '../components/OrderMaterialFormModal';
import { OrderMaterialTable } from '../components/OrderMaterialTable';
import { PendingMaterialOrdersTable } from '../components/PendingMaterialOrdersTable';
import { INCOMING_TEXT, LABELS } from '../constants';
import { useDeleteOrderMaterial, useOrderMaterials } from '../hooks/useOrderMaterials';
import type { OrderMaterial } from '../types';

/**
 * UC09 (phần NVL): Admin / Quản đốc khai báo NVL khách cấp, phải mua của đơn. Số cần theo định mức đọc từ
 * định mức của mã hàng (màn Mã hàng).
 * Màn chính là danh sách đơn chờ khai báo / mua NVL; bấm Mở đơn mới sang NVL của đơn đó (đơn nằm trên đường dẫn ?orderId=).
 */
export function OrderMaterialPage() {
  // Đơn đang xem nằm trên đường dẫn (?orderId=) để bấm thông báo "Có đơn hàng mới" là mở thẳng đơn đó.
  const [params, setParams] = useSearchParams();
  const orderId = Number(params.get('orderId')) || undefined;
  const setOrderId = (id: number | undefined) =>
    setParams(id === undefined ? {} : { orderId: String(id) });
  const list = useOrderMaterials(orderId);
  const remove = useDeleteOrderMaterial();
  const [editing, setEditing] = useState<OrderMaterial | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [bulkEditing, setBulkEditing] = useState(false);
  const orderCode = useOrderOptions().data?.find((o) => o.orderId === orderId)?.orderCode;

  const openForm = (om: OrderMaterial | null) => {
    setEditing(om);
    setFormOpen(true);
  };

  const columns = orderMaterialColumns({
    onEdit: openForm,
    onDelete: (om) => remove.mutate(om.orderMaterialId),
  });

  if (bulkEditing) {
    return (
      <PageCard title="Sửa NVL của cả đơn" onBack={() => setBulkEditing(false)}>
        <OrderMaterialBulkEditor
          orderId={orderId as number}
          materials={list.data ?? []}
          onDone={() => setBulkEditing(false)}
        />
      </PageCard>
    );
  }

  if (orderId === undefined) {
    return (
      <PageCard title={INCOMING_TEXT.pendingTitle} description={INCOMING_TEXT.pendingHint}>
        <PendingMaterialOrdersTable onPick={setOrderId} />
      </PageCard>
    );
  }

  return (
    <PageCard
      title={orderCode ? `${LABELS.orderMaterialsTitle}: ${orderCode}` : LABELS.orderMaterialsTitle}
      onBack={() => setOrderId(undefined)}
      extra={
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openForm(null)}>
          {MESSAGES.common.create}
        </Button>
      }
    >
      <OrderMaterialTable
        key={orderId}
        orderId={orderId}
        columns={columns}
        actions={
          (list.data?.length ?? 0) > 0 && (
            <Button icon={<EditOutlined />} onClick={() => setBulkEditing(true)}>
              Sửa cả đơn
            </Button>
          )
        }
      />
      <OrderMaterialFormModal
        open={formOpen}
        orderId={orderId}
        orderMaterial={editing}
        onClose={() => setFormOpen(false)}
      />
    </PageCard>
  );
}
