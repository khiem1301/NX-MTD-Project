import { useState } from 'react';
import { Button, Card, Input, Select, Space, Table, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useTableQuery } from '@/hooks/useTableQuery';
import type { WorkOrderListItem } from '@/types/workOrder';
import { formatDate } from '@/utils/format';
import { WorkOrderDetailDrawer } from '../components/WorkOrderDetailDrawer';
import { WorkOrderFormModal } from '../components/WorkOrderFormModal';
import { LABELS, WORK_ORDER_STATUS_LABEL } from '../constants';
import { useWorkOrders } from '../hooks/useCutting';
import type { WorkOrder } from '../types';

type Filters = { keyword?: string; status?: string };

const STATUS_OPTIONS = Object.entries(WORK_ORDER_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));

/** UC19 (tổ cắt): Quản đốc lập lệnh cắt; Thủ kho và tổ cắt xem để cấp vải / cắt. */
export function WorkOrderListPage() {
  const table = useTableQuery<Filters>({});
  const workOrders = useWorkOrders(table.params);
  const [viewingId, setViewingId] = useState<number>();
  const [editing, setEditing] = useState<WorkOrder | null>(null);
  const [formOpen, setFormOpen] = useState(false);

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
          <Button type="primary" onClick={() => openForm(null)}>
            {MESSAGES.common.create}
          </Button>
        </Can>
      }
    >
      <Space style={{ marginBottom: 16 }} wrap>
        <Input.Search
          allowClear
          placeholder={MESSAGES.common.search}
          onSearch={(keyword) => table.setFilters({ keyword: keyword || undefined })}
          style={{ width: 280 }}
        />
        <Select
          allowClear
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
      <WorkOrderDetailDrawer
        workOrderId={viewingId}
        onClose={() => setViewingId(undefined)}
        onEdit={openForm}
      />
      <WorkOrderFormModal open={formOpen} workOrder={editing} onClose={() => setFormOpen(false)} />
    </Card>
  );
}
