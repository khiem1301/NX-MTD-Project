import { useState } from 'react';
import { Alert, Button, Card, Space, Table, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { WorkOrderSelect } from '@/components/common/WorkOrderSelect';
import { MESSAGES } from '@/constants/messages';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDate } from '@/utils/format';
import { LayFormModal } from '../components/LayFormModal';
import { LABELS, LAY_STATUS, LAY_STATUS_LABEL, SHIFT_LABEL, WORK_ORDER_STATUS } from '../constants';
import { useLays, useWorkOrder, useWorkOrders } from '../hooks/useCutting';
import type { CuttingLay } from '../types';

const CUTTABLE: readonly string[] = [WORK_ORDER_STATUS.OPEN, WORK_ORDER_STATUS.CUTTING];

/** UC51: tổ trưởng / tổ phó cắt chọn lệnh cắt của tổ và nhập từng bàn cắt. */
export function CuttingLayPage() {
  const [workOrderId, setWorkOrderId] = useState<number>();
  const workOrders = useWorkOrders({ page: 1, pageSize: MAX_PAGE_SIZE });
  const workOrder = useWorkOrder(workOrderId);
  const table = useTableQuery<{ workOrderId?: number }>({});
  const lays = useLays({ ...table.params, workOrderId });
  const [editing, setEditing] = useState<CuttingLay | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const cuttable = workOrder.data !== undefined && CUTTABLE.includes(workOrder.data.status);

  const openForm = (lay: CuttingLay | null) => {
    setEditing(lay);
    setFormOpen(true);
  };

  const columns: TableColumnsType<CuttingLay> = [
    { title: LABELS.workDate, dataIndex: 'workDate', render: formatDate },
    { title: LABELS.shift, dataIndex: 'shiftCode', render: (v: string) => SHIFT_LABEL[v] ?? v },
    { title: LABELS.laySequence, dataIndex: 'laySequence', align: 'right' },
    { title: LABELS.fabricColor, dataIndex: 'fabricColor' },
    { title: LABELS.plyCount, dataIndex: 'plyCount', align: 'right' },
    { title: LABELS.sizeRatio, dataIndex: 'sizeRatio' },
    {
      title: LABELS.actualQty,
      key: 'total',
      align: 'right',
      render: (_, l) => l.lines.reduce((s, x) => s + x.actualQty, 0),
    },
    {
      title: LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={LAY_STATUS_LABEL} />,
    },
    {
      title: MESSAGES.common.actions,
      key: 'actions',
      render: (_, l) =>
        l.status === LAY_STATUS.RECORDED && cuttable ? (
          <Button size="small" onClick={() => openForm(l)}>
            {MESSAGES.common.edit}
          </Button>
        ) : null,
    },
  ];

  return (
    <Card
      title={LABELS.laysTitle}
      extra={
        <Space>
          <WorkOrderSelect
            workOrders={workOrders.data?.items.filter((w) => CUTTABLE.includes(w.status))}
            loading={workOrders.isLoading}
            value={workOrderId}
            onChange={setWorkOrderId}
            placeholder={LABELS.workOrder}
          />
          <Button type="primary" disabled={!cuttable} onClick={() => openForm(null)}>
            {MESSAGES.common.create}
          </Button>
        </Space>
      }
    >
      {workOrderId === undefined && (
        <Alert
          type="info"
          showIcon
          message={LABELS.selectWorkOrderHint}
          style={{ marginBottom: 16 }}
        />
      )}
      <QueryError error={lays.error ?? workOrder.error} onRetry={() => void lays.refetch()} />
      <Table
        rowKey="cuttingLayId"
        columns={columns}
        dataSource={workOrderId === undefined ? [] : lays.data?.items}
        loading={lays.isFetching}
        pagination={table.pagination(lays.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
        expandable={{
          expandedRowRender: (l) =>
            l.lines
              .map(
                (x) =>
                  `${x.color}-${x.sizeCode}: ${x.actualQty}${x.adjustReason ? ` (${x.adjustReason})` : ''}`,
              )
              .join(' · '),
        }}
      />
      {workOrder.data && (
        <LayFormModal
          open={formOpen}
          workOrder={workOrder.data}
          lay={editing}
          onClose={() => setFormOpen(false)}
        />
      )}
    </Card>
  );
}
