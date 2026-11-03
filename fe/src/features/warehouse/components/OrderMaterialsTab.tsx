import { useState } from 'react';
import { EditOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Space } from 'antd';
import { LABELS } from '../constants';
import { useDeleteOrderMaterial, useOrderMaterials } from '../hooks/useOrderMaterials';
import type { OrderMaterial } from '../types';
import { orderMaterialColumns } from './orderMaterialColumns';
import { OrderMaterialBulkEditor } from './OrderMaterialBulkEditor';
import { OrderMaterialByProductModal } from './OrderMaterialByProductModal';
import { OrderMaterialFormModal } from './OrderMaterialFormModal';
import { OrderMaterialTable } from './OrderMaterialTable';

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

  const openForm = (om: OrderMaterial | null) => {
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
              <Button icon={<PlusOutlined />} onClick={() => openForm(null)}>
                {LABELS.addExternalMaterial}
              </Button>
              <Button type="primary" icon={<PlusOutlined />} onClick={() => setByProductOpen(true)}>
                {LABELS.addByProduct}
              </Button>
            </Space>
          )
        }
      />
      <OrderMaterialFormModal
        open={formOpen}
        orderId={orderId}
        orderMaterial={editing}
        declared={list.data ?? []}
        onClose={() => setFormOpen(false)}
      />
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
