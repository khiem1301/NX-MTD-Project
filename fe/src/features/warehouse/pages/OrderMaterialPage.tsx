import { useState } from 'react';
import { Button, Card, Popconfirm, Space, Table, Tag, type TableColumnsType } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { formatDate, formatQty } from '@/utils/format';
import { OrderMaterialFormModal } from '../components/OrderMaterialFormModal';
import { PurchaseOrderFormModal } from '../components/PurchaseOrderFormModal';
import { StockPurchaseTable } from '../components/StockPurchaseTable';
import { LABELS, MATERIAL_TYPE_LABEL, SOURCE_LABEL } from '../constants';
import { useDeleteOrderMaterial, useOrderMaterials } from '../hooks/useOrderMaterials';
import type { OrderMaterial, OrderMaterialNorm } from '../types';

const normColumns: TableColumnsType<OrderMaterialNorm> = [
  { title: LABELS.product, dataIndex: 'productCode' },
  { title: LABELS.component, dataIndex: 'componentName', render: (v: string) => v || '—' },
  { title: LABELS.garmentColor, dataIndex: 'garmentColor', render: (v: string | null) => v ?? '—' },
  { title: LABELS.sizeCode, dataIndex: 'sizeCode', render: (v: string | null) => v ?? '—' },
  { title: LABELS.qtyPerPiece, dataIndex: 'qtyPerPiece', align: 'right', render: formatQty },
  {
    title: LABELS.wastagePercent,
    dataIndex: 'wastagePercent',
    align: 'right',
    render: (v: number) => `${v}%`,
  },
];

/**
 * UC09 (phần NVL): Admin / Quản đốc khai báo NVL khách cấp, phải mua và định mức của đơn.
 * Quản đốc chưa chọn đơn vẫn bấm Tạo được: đó là đơn mua NVL dùng chung (kim, chỉ...), không gắn mã đơn hàng.
 */
export function OrderMaterialPage() {
  const { hasRole } = usePermission();
  const [orderId, setOrderId] = useState<number>();
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
    {
      title: LABELS.normRequiredQty,
      dataIndex: 'normRequiredQty',
      align: 'right',
      render: formatQty,
    },
    { title: LABELS.customerQty, dataIndex: 'customerQty', align: 'right', render: formatQty },
    { title: LABELS.customerExpectedDate, dataIndex: 'customerExpectedDate', render: formatDate },
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
        <Space>
          <Button size="small" onClick={() => openForm(om)}>
            {MESSAGES.common.edit}
          </Button>
          <Popconfirm
            title={MESSAGES.warehouse.confirmDeleteOrderMaterial}
            onConfirm={() => remove.mutate(om.orderMaterialId)}
          >
            <Button size="small" danger>
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
        <Space>
          <OrderSelect
            allowClear
            value={orderId}
            onChange={setOrderId}
            placeholder={hasRole(ROLES.Manager) ? LABELS.selectOrderOrStock : LABELS.selectOrder}
            width={380}
          />
          <Button
            type="primary"
            disabled={orderId === undefined && !stockMode}
            onClick={() => (stockMode ? setStockFormOpen(true) : openForm(null))}
          >
            {MESSAGES.common.create}
          </Button>
        </Space>
      }
    >
      {stockMode ? (
        <StockPurchaseTable />
      ) : (
        <>
          <QueryError error={list.error} onRetry={() => void list.refetch()} />
          <Table
            rowKey="orderMaterialId"
            columns={columns}
            dataSource={list.data}
            loading={list.isFetching}
            pagination={false}
            scroll={{ x: true }}
            locale={{
              emptyText: orderId === undefined ? LABELS.selectOrder : MESSAGES.common.empty,
            }}
            expandable={{
              rowExpandable: (om) => om.norms.length > 0,
              expandedRowRender: (om) => (
                <Table
                  rowKey="orderMaterialNormId"
                  size="small"
                  columns={normColumns}
                  dataSource={om.norms}
                  pagination={false}
                />
              ),
            }}
          />
        </>
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
