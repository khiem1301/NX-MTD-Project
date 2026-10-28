import { useState } from 'react';
import { Alert, Button, Card, Space, Table, Tabs, Tag, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useTableQuery } from '@/hooks/useTableQuery';
import type { WorkOrderListItem } from '@/types/workOrder';
import { formatDate } from '@/utils/format';
import { WorkOrderDetailDrawer } from '../components/WorkOrderDetailDrawer';
import { ReplacementWorkOrderModal } from '../components/ReplacementWorkOrderModal';
import { WorkOrderFormModal } from '../components/WorkOrderFormModal';
import { LABELS, WORK_ORDER_STATUS_LABEL } from '../constants';
import { useFabricByOrder, useWorkOrders } from '../hooks/useCutting';
import type { FabricByOrderRow, WorkOrder } from '../types';
import { usePermission } from '@/hooks/usePermission';
import { formatQty } from '@/utils/format';
import { DebouncedSearch } from '@/components/common/DebouncedSearch';
import { FilterSelect } from '@/components/common/FilterSelect';

type Filters = { keyword?: string; status?: string };

const STATUS_OPTIONS = Object.entries(WORK_ORDER_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

/** Đơn nào đang dùng vải nào (Quản đốc): gom các lệnh cắt chưa hủy theo đơn × vải. */
function FabricByOrderTab() {
  const [orderCode, setOrderCode] = useState<string>();
  const rows = useFabricByOrder();
  const data = (rows.data ?? []).filter((r) => !orderCode || r.orderCode.toLowerCase().includes(orderCode.toLowerCase()));
  const columns: TableColumnsType<FabricByOrderRow> = [
    { title: LABELS.order, dataIndex: 'orderCode' },
    {
      title: LABELS.fabricPreviewMaterial,
      key: 'material',
      render: (_, r) => `${r.materialCode} ${r.materialName}${r.materialColor ? ` (${r.materialColor})` : ''}`,
    },
    { title: LABELS.fabricNeed, dataIndex: 'requiredQty', align: 'right', render: (v: number, r) => `${formatQty(v)} ${r.unit}` },
    { title: LABELS.fabricIssued, dataIndex: 'issuedQty', align: 'right', render: (v: number) => formatQty(v) },
    {
      title: LABELS.fabricRemaining,
      dataIndex: 'remainingQty',
      align: 'right',
      render: (v: number) => (v > 0 ? <Tag color="orange">{formatQty(v)}</Tag> : <Tag color="green">0</Tag>),
    },
    { title: LABELS.fabricWorkOrders, dataIndex: 'workOrderCodes', render: (codes: string[]) => codes.join(', ') },
  ];
  return (
    <>
      <Alert type="info" showIcon message={LABELS.fabricByOrderHint} style={{ marginBottom: 16 }} />
      <Space style={{ marginBottom: 16 }}>
        <DebouncedSearch allowClear placeholder={LABELS.order} style={{ width: 240 }} onSearch={(k) => setOrderCode(k.trim() || undefined)} />
      </Space>
      <QueryError error={rows.error} onRetry={() => void rows.refetch()} />
      <Table rowKey={(r) => `${r.orderId}-${r.materialId}`} columns={columns} dataSource={data} loading={rows.isFetching}
        pagination={false} locale={{ emptyText: MESSAGES.common.empty }} />
    </>
  );
}

/** UC19 (tổ cắt): Quản đốc lập lệnh cắt; Thủ kho và tổ cắt xem để cấp vải / cắt. */
export function WorkOrderListPage() {
  const { hasRole } = usePermission();
  const isManager = hasRole(ROLES.Manager);
  const [tab, setTab] = useState('orders');
  const table = useTableQuery<Filters>({});
  const workOrders = useWorkOrders(table.params);
  const [viewingId, setViewingId] = useState<number>();
  const [editing, setEditing] = useState<WorkOrder | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [replacementOpen, setReplacementOpen] = useState(false);

  const openForm = (workOrder: WorkOrder | null) => {
    setViewingId(undefined);
    setEditing(workOrder);
    setFormOpen(true);
  };

  const columns: TableColumnsType<WorkOrderListItem> = [
    {
      title: LABELS.workOrderCode,
      dataIndex: 'workOrderCode',
      render: (v: string, w) => <a onClick={() => setViewingId(w.workOrderId)}>{v}</a>,
    },
    { title: LABELS.order, dataIndex: 'orderCode' },
    { title: LABELS.product, dataIndex: 'productCode' },
    { title: LABELS.cuttingTeam, dataIndex: 'cuttingTeamName' },
    { title: LABELS.totalPlannedQty, dataIndex: 'totalPlannedQty', align: 'right' },
    { title: LABELS.dueDate, dataIndex: 'dueDate', render: formatDate },
    {
      title: LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={WORK_ORDER_STATUS_LABEL} />,
    },
  ];

  return (
    <Card
      title={LABELS.workOrdersTitle}
      extra={
        <Can roles={[ROLES.Manager]}>
          <Space>
            <Button onClick={() => setReplacementOpen(true)}>{LABELS.createReplacement}</Button>
            <Button type="primary" onClick={() => openForm(null)}>
              {MESSAGES.common.create}
            </Button>
          </Space>
        </Can>
      }
    >
      {isManager && (
        <Tabs
          activeKey={tab}
          onChange={setTab}
          items={[
            { key: 'orders', label: LABELS.workOrdersTab },
            { key: 'fabric', label: LABELS.fabricByOrderTab },
          ]}
        />
      )}
      {isManager && tab === 'fabric' ? <FabricByOrderTab /> : (<>
      <Space style={{ marginBottom: 16 }} wrap>
        <DebouncedSearch
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 280 }}
        />
        <FilterSelect value={table.filters.status}
          placeholder={LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 160 }}
        />
      </Space>
      <QueryError error={workOrders.error} onRetry={() => void workOrders.refetch()} />
      <Table
        rowKey="workOrderId"
        columns={columns}
        dataSource={workOrders.data?.items}
        loading={workOrders.isFetching}
        pagination={table.pagination(workOrders.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
      />
      </>)}
      <WorkOrderDetailDrawer
        workOrderId={viewingId}
        onClose={() => setViewingId(undefined)}
        onEdit={openForm}
      />
      <ReplacementWorkOrderModal open={replacementOpen} onClose={() => setReplacementOpen(false)} />
      <WorkOrderFormModal open={formOpen} workOrder={editing} onClose={() => setFormOpen(false)} />
    </Card>
  );
}
