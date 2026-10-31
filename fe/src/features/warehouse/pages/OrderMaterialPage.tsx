import { useState } from 'react';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import { useSearchParams } from 'react-router-dom';
import { Button, Card, Popconfirm, Space, Tag, type TableColumnsType } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { formatQty } from '@/utils/format';
import { OrderMaterialFormModal } from '../components/OrderMaterialFormModal';
import { PendingMaterialOrdersTable } from '../components/PendingMaterialOrdersTable';
import { PurchaseOrderFormModal } from '../components/PurchaseOrderFormModal';
import { StockPurchaseTable } from '../components/StockPurchaseTable';
import { LABELS, MATERIAL_TYPE_LABEL, SOURCE_LABEL } from '../constants';
import { useDeleteOrderMaterial, useOrderMaterials } from '../hooks/useOrderMaterials';
import type { OrderMaterial } from '../types';
import { DataTable } from '@/components/common/DataTable';

/**
 * UC09 (phần NVL): Admin / Quản đốc khai báo NVL khách cấp, phải mua của đơn. Số cần theo định mức đọc từ
 * định mức của mã hàng (màn Mã hàng).
 * Quản đốc chưa chọn đơn vẫn bấm Tạo được: đó là đơn mua NVL dùng chung (kim, chỉ...), không gắn mã đơn hàng.
 */
export function OrderMaterialPage() {
  const { hasRole } = usePermission();
  // Đơn đang xem nằm trên đường dẫn (?orderId=) để bấm thông báo "Có đơn hàng mới" là mở thẳng đơn đó.
  const [params, setParams] = useSearchParams();
  const orderId = Number(params.get('orderId')) || undefined;
  const setOrderId = (id: number | undefined) =>
    setParams(id === undefined ? {} : { orderId: String(id) });
  const list = useOrderMaterials(orderId);
  const remove = useDeleteOrderMaterial();
  const [editing, setEditing] = useState<OrderMaterial | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [stockFormOpen, setStockFormOpen] = useState(false);
  const stockMode = orderId === undefined && hasRole(ROLES.Manager);

  const openForm = (om: OrderMaterial | null) => {
    setEditing(om);
    setFormOpen(true);
  };

  const columns: TableColumnsType<OrderMaterial> = [
    {
      title: LABELS.material,
      key: 'material',
      render: (_, om) => (
        <>
          <b>{om.materialCode}</b> {om.materialName}{' '}
          <Tag>{MATERIAL_TYPE_LABEL[om.materialType]}</Tag>
        </>
      ),
    },
    { title: LABELS.color, dataIndex: 'materialColor', render: (v: string) => v || '—' },
    { title: LABELS.unit, dataIndex: 'unit' },
    { title: LABELS.requiredQty, dataIndex: 'requiredQty', align: 'right', render: formatQty },
    { title: LABELS.customerQty, dataIndex: 'customerQty', align: 'right', render: formatQty },
    {
      title: LABELS.deliveries,
      key: 'deliveries',
      render: (_, om) =>
        om.expectedDeliveryCount === null
          ? '—'
          : `${om.receivedCount} / ${om.expectedDeliveryCount} ${LABELS.deliveryUnit}`,
    },
    { title: LABELS.purchaseQty, dataIndex: 'purchaseQty', align: 'right', render: formatQty },
    {
      title: LABELS.source,
      dataIndex: 'source',
      render: (v: string | null) => (v ? SOURCE_LABEL[v] : '—'),
    },
    { title: LABELS.receivedQty, dataIndex: 'receivedQty', align: 'right', render: formatQty },
    {
      title: MESSAGES.common.actions,
      key: 'actions',
      render: (_, om) => (
        <Space size={0} wrap>
          <Button type="link" icon={<EditOutlined />} onClick={() => openForm(om)}>
            {MESSAGES.common.edit}
          </Button>
          <Popconfirm
            title={MESSAGES.warehouse.confirmDeleteOrderMaterial}
            onConfirm={() => remove.mutate(om.orderMaterialId)}
          >
            <Button type="link" icon={<DeleteOutlined />} danger>
              {MESSAGES.common.delete}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Card
      title={LABELS.orderMaterialsTitle}
      extra={
        <Button
          type="primary"
          icon={<PlusOutlined />}
          disabled={orderId === undefined && !stockMode}
          onClick={() => (stockMode ? setStockFormOpen(true) : openForm(null))}
        >
          {MESSAGES.common.create}
        </Button>
      }
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <OrderSelect
          allowClear
          value={orderId}
          onChange={setOrderId}
          placeholder={hasRole(ROLES.Manager) ? LABELS.selectOrderOrStock : LABELS.selectOrder}
          width={380}
        />
      </Space>
      {orderId === undefined && <PendingMaterialOrdersTable onPick={setOrderId} />}
      {stockMode ? (
        <StockPurchaseTable stockOnly />
      ) : (
        // Chưa chọn đơn: chỉ còn danh sách đơn chờ khai báo ở trên, không dựng bảng chi tiết trống.
        orderId !== undefined && (
          <>
            <QueryError error={list.error} onRetry={() => void list.refetch()} />
            <DataTable
              rowKey="orderMaterialId"
              columns={columns}
              dataSource={list.data}
              loading={list.isFetching}
              pagination={false}
              scroll={{ x: true }}
            />
          </>
        )
      )}
      <PurchaseOrderFormModal open={stockFormOpen} onClose={() => setStockFormOpen(false)} />
      {orderId !== undefined && (
        <OrderMaterialFormModal
          open={formOpen}
          orderId={orderId}
          orderMaterial={editing}
          onClose={() => setFormOpen(false)}
        />
      )}
    </Card>
  );
}
