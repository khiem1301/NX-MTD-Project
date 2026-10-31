import { useState } from 'react';
import { PlusOutlined } from '@ant-design/icons';
import { Button, Card, type TableColumnsType } from 'antd';
import { Can } from '@/components/guards/Can';
import { QueryError } from '@/components/common/QueryError';
import { StatusTag } from '@/components/common/StatusTag';
import { WorkOrderSelect } from '@/components/common/WorkOrderSelect';
import { MAX_PAGE_SIZE } from '@/constants/pagination';
import { ROLES } from '@/constants/roles';
import { usePermission } from '@/hooks/usePermission';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import { HandoverCreateModal } from '../components/HandoverCreateModal';
import { HandoverDetailModal } from '../components/HandoverDetailModal';
import { HANDOVER_STATUS_LABEL, LABELS, WORK_ORDER_STATUS } from '../constants';
import { useHandovers, useWorkOrders } from '../hooks/useCutting';
import type { CutHandoverListItem } from '../types';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DetailLink } from '@/components/common/DetailLink';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';

type Filters = { status?: string; workOrderId?: number };

const STATUS_OPTIONS = Object.entries(HANDOVER_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));
const CUTTABLE: readonly string[] = [WORK_ORDER_STATUS.OPEN, WORK_ORDER_STATUS.CUTTING];

/** UC43: tổ cắt lập phiếu bàn giao BTP; Thủ kho xác nhận nhận vào kho BTP. */
export function CutHandoverPage() {
  const { hasRole } = usePermission();
  const isCuttingLeader = hasRole(ROLES.TeamLeader, ROLES.TeamDeputy);
  const table = useTableQuery<Filters>({});
  const handovers = useHandovers(table.params);
  const workOrders = useWorkOrders({ page: 1, pageSize: MAX_PAGE_SIZE });
  const [viewingId, setViewingId] = useState<number>();
  const [createOpen, setCreateOpen] = useState(false);

  const columns: TableColumnsType<CutHandoverListItem> = [
    {
      title: LABELS.handoverCode,
      dataIndex: 'handoverCode',
      render: (v: string, h) => (
        <DetailLink onClick={() => setViewingId(h.cutHandoverId)}>{v}</DetailLink>
      ),
    },
    { title: LABELS.workOrder, dataIndex: 'workOrderCode' },
    { title: LABELS.order, dataIndex: 'orderCode' },
    { title: LABELS.cuttingTeam, dataIndex: 'cuttingTeamName' },
    { title: LABELS.totalHandedQty, dataIndex: 'totalHandedQty', align: 'right' },
    { title: LABELS.handedAt, dataIndex: 'handedAt', render: formatDateTime },
    {
      title: LABELS.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={HANDOVER_STATUS_LABEL} />,
    },
    {
      title: LABELS.confirmedBy,
      dataIndex: 'confirmedByName',
      render: (v: string | null) => v ?? '—',
    },
  ];

  return (
    <Card
      title={LABELS.handoversTitle}
      extra={
        <Can roles={[ROLES.TeamLeader, ROLES.TeamDeputy]}>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            disabled={table.filters.workOrderId === undefined}
            onClick={() => setCreateOpen(true)}
          >
            {LABELS.createHandover}
          </Button>
        </Can>
      }
    >
      <TableToolbar
        filters={
          <>
            <WorkOrderSelect
              workOrders={
                isCuttingLeader
                  ? workOrders.data?.items.filter((w) => CUTTABLE.includes(w.status))
                  : workOrders.data?.items
              }
              loading={workOrders.isLoading}
              value={table.filters.workOrderId}
              onChange={(workOrderId) => table.setFilters({ workOrderId })}
              placeholder={LABELS.workOrder}
            />
            <FilterSelect
              value={table.filters.status}
              placeholder={LABELS.status}
              options={STATUS_OPTIONS}
              onChange={(status?: string) => table.setFilters({ status })}
              style={{ width: 170 }}
            />
          </>
        }
      />
      <QueryError error={handovers.error} onRetry={() => void handovers.refetch()} />
      <DataTable
        rowKey="cutHandoverId"
        columns={columns}
        dataSource={handovers.data?.items}
        loading={handovers.isFetching}
        pagination={table.pagination(handovers.data?.totalItems)}
      />
      <HandoverDetailModal handoverId={viewingId} onClose={() => setViewingId(undefined)} />
      {table.filters.workOrderId !== undefined && (
        <HandoverCreateModal
          open={createOpen}
          workOrderId={table.filters.workOrderId}
          onClose={() => setCreateOpen(false)}
        />
      )}
    </Card>
  );
}
