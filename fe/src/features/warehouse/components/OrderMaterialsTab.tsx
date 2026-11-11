import { useState } from 'react';
import { EditOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Space } from 'antd';
import { LABELS } from '../constants';
import { useDeleteOrderMaterial, useOrderMaterials } from '../hooks/useOrderMaterials';
import type { OrderMaterial } from '../types';
import { orderMaterialColumns } from './orderMaterialColumns';
import { OrderMaterialBulkEditor } from './OrderMaterialBulkEditor';
import { OrderMaterialByProductModal } from './OrderMaterialByProductModal';
import { OrderMaterialExternalModal } from './OrderMaterialExternalModal';
import { OrderMaterialFormModal } from './OrderMaterialFormModal';
import { OrderMaterialTable } from './OrderMaterialTable';
import { CustomerDeliveryPlan } from './CustomerDeliveryPlan';

type OrderMaterialsTabProps = {
  orderId: number;
  /** Đơn đã đóng: chỉ xem (BE cũng chặn thêm, sửa, xóa). */
  isClosed: boolean;
};

/** UC09 (phần NVL): tab trong chi tiết đơn, Admin / Quản đốc khai báo NVL khách cấp, phải mua sau khi đơn đã tạo. */
export function OrderMaterialsTab({ orderId, isClosed }: OrderMaterialsTabProps) {
  const list = useOrderMaterials(orderId);
  const remove = useDeleteOrderMaterial();
  const [editing, setEditing] = useState<OrderMaterial | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [bulkEditing, setBulkEditing] = useState(false);
  const [byProductOpen, setByProductOpen] = useState(false);
  const [externalOpen, setExternalOpen] = useState(false);

  const openForm = (om: OrderMaterial) => {
    setEditing(om);
    setFormOpen(true);
  };

  if (bulkEditing) {
    return (
      <OrderMaterialBulkEditor
        orderId={orderId}
        materials={list.data ?? []}
        onDone={() => setBulkEditing(false)}
      />
    );
  }

  const columns = orderMaterialColumns(
    isClosed
      ? undefined
      : { onEdit: openForm, onDelete: (om) => remove.mutate(om.orderMaterialId) },
  );

  return (
    <>
      <CustomerDeliveryPlan orderId={orderId} isClosed={isClosed} />
      <OrderMaterialTable
        orderId={orderId}
        columns={columns}
        actions={
          !isClosed && (
            <Space>
              {(list.data?.length ?? 0) > 0 && (
                <Button icon={<EditOutlined />} onClick={() => setBulkEditing(true)}>
                  {LABELS.bulkEditOrderMaterials}
                </Button>
              )}
              <Button icon={<PlusOutlined />} onClick={() => setExternalOpen(true)}>
                {LABELS.addExternalMaterial}
              </Button>
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setByProductOpen(true)}>
                {LABELS.addByProduct}
              </Button>
            </Space>
          )
        }
      />
      {editing && (
        <OrderMaterialFormModal
          open={formOpen}
          orderId={orderId}
          orderMaterial={editing}
          declared={list.data ?? []}
          onClose={() => setFormOpen(false)}
        />
      )}
      {externalOpen && (
        <OrderMaterialExternalModal
          orderId={orderId}
          declared={list.data ?? []}
          onClose={() => setExternalOpen(false)}
        />
      )}
      {byProductOpen && (
        <OrderMaterialByProductModal
          orderId={orderId}
          declared={list.data ?? []}
          onClose={() => setByProductOpen(false)}
        />
      )}
    </>
  );
}
