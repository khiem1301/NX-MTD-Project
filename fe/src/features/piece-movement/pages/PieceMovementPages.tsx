import { useState } from 'react';
import { Button, Card, Space, Table, Tabs, Tag, Typography, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { ROLES } from '@/constants/roles';
import { useAuth } from '@/hooks/useAuth';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import { AllocationBoard } from '../components/AllocationBoard';
import { ConfirmMovementModal } from '../components/ConfirmMovementModal';
import {
  CONFIRM_MODE_LABEL,
  MOVEMENT_STATUS,
  MOVEMENT_STATUS_LABEL,
  MOVEMENT_TYPE_LABEL,
  PM_LABELS,
  RETRIEVAL_REASON_LABEL,
} from '../constants';
import { useAllocations, useRejectByWorker } from '../hooks/usePieceMovement';
import type { WorkAllocation } from '../types';
import { FilterSelect } from '@/components/common/FilterSelect';

type Filters = { status?: string; movementType?: string };

const STATUS_OPTIONS = Object.entries(MOVEMENT_STATUS_LABEL).map(([value, l]) => ({
  value,
  label: l.text,
}));
const TYPE_OPTIONS = Object.entries(MOVEMENT_TYPE_LABEL).map(([value, label]) => ({
  value,
  label,
}));

function useAllocationColumns(
  extra: (m: WorkAllocation) => React.ReactNode,
  withWorker: boolean,
): TableColumnsType<WorkAllocation> {
  return [
    {
      title: PM_LABELS.type,
      dataIndex: 'movementType',
      render: (v: string, m) => (
        <Space direction="vertical" size={0}>
          <Tag color={v === 'HANDOVER' ? 'blue' : 'orange'}>{MOVEMENT_TYPE_LABEL[v]}</Tag>
          {m.retrievalReason && (
            <Typography.Text type="secondary">
              {RETRIEVAL_REASON_LABEL[m.retrievalReason]}
            </Typography.Text>
          )}
        </Space>
      ),
    },
    ...(withWorker
      ? [
          {
            title: PM_LABELS.worker,
            key: 'worker',
            render: (_: unknown, m: WorkAllocation) => `${m.workerCode} - ${m.workerName}`,
          },
        ]
      : []),
    {
      title: PM_LABELS.orderLine,
      key: 'line',
      render: (_, m) => `${m.orderCode} · ${m.productCode} ${m.color}-${m.sizeCode}`,
    },
    {
      title: PM_LABELS.operation,
      key: 'operation',
      render: (_, m) => `${m.operationCode} - ${m.operationName}`,
    },
    { title: PM_LABELS.quantity, dataIndex: 'quantity', align: 'right' },
    {
      title: PM_LABELS.confirmedQty,
      dataIndex: 'confirmedQty',
      align: 'right',
      render: (v: number | null) => v ?? '—',
    },
    {
      title: PM_LABELS.status,
      dataIndex: 'status',
      render: (v: string, m) => (
        <Space direction="vertical" size={0}>
          <StatusTag status={v} labels={MOVEMENT_STATUS_LABEL} />
          {v === MOVEMENT_STATUS.CONFIRMED && (
            <Typography.Text type="secondary">{CONFIRM_MODE_LABEL[m.confirmMode]}</Typography.Text>
          )}
          {m.rejectReason && <Typography.Text type="danger">{m.rejectReason}</Typography.Text>}
        </Space>
      ),
    },
    { title: PM_LABELS.createdAt, dataIndex: 'createdAt', render: formatDateTime },
    { title: MESSAGES.common.actions, key: 'action', render: (_, m) => extra(m) },
  ];
}

function AllocationHistory() {
  const table = useTableQuery<Filters>({});
  const allocations = useAllocations(table.params);
  const [onBehalf, setOnBehalf] = useState<WorkAllocation | null>(null);

  const columns = useAllocationColumns(
    (m) =>
      m.status === MOVEMENT_STATUS.PENDING ? (
        <Button type="link" onClick={() => setOnBehalf(m)}>
          {PM_LABELS.confirmOnBehalf}
        </Button>
      ) : null,
    true,
  );

  return (
    <>
      <Space style={{ marginBottom: 16 }} wrap>
        <FilterSelect value={table.filters.movementType}
          placeholder={PM_LABELS.type}
          options={TYPE_OPTIONS}
          onChange={(movementType?: string) => table.setFilters({ movementType })}
          style={{ width: 170 }}
        />
        <FilterSelect value={table.filters.status}
          placeholder={PM_LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 220 }}
        />
      </Space>
      <QueryError error={allocations.error} onRetry={() => void allocations.refetch()} />
      <Table
        rowKey="workAllocationId"
        columns={columns}
        dataSource={allocations.data?.items}
        loading={allocations.isFetching}
        pagination={table.pagination(allocations.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
      />
      <ConfirmMovementModal movement={onBehalf} onBehalf onClose={() => setOnBehalf(null)} />
    </>
  );
}

/** UC53 / UC54: tổ trưởng / tổ phó may chia việc cho công nhân theo công đoạn, xác nhận hộ khi công nhân chưa xác nhận. */
export function LeaderPieceMovementsPage() {
  const { user } = useAuth();
  const canEdit = user?.role === ROLES.TeamLeader || user?.role === ROLES.TeamDeputy;
  return (
    <Card title={PM_LABELS.leaderTitle}>
      <Tabs
        items={[
          {
            key: 'board',
            label: PM_LABELS.boardTab,
            children: <AllocationBoard canEdit={canEdit} />,
          },
          { key: 'history', label: PM_LABELS.historyTab, children: <AllocationHistory /> },
        ]}
      />
    </Card>
  );
}

/** UC60: công nhân xem việc được giao / thu hồi của mình rồi tự xác nhận hoặc từ chối. */
export function MyPieceMovementsPage() {
  const table = useTableQuery<Filters>({});
  const allocations = useAllocations(table.params);
  const reject = useRejectByWorker();
  const [confirming, setConfirming] = useState<WorkAllocation | null>(null);
  const [rejecting, setRejecting] = useState<WorkAllocation | null>(null);

  const columns = useAllocationColumns(
    (m) =>
      m.status === MOVEMENT_STATUS.PENDING ? (
        <Space size={0}>
          <Button type="link" onClick={() => setConfirming(m)}>
            {PM_LABELS.confirm}
          </Button>
          <Button type="link" danger onClick={() => setRejecting(m)}>
            {PM_LABELS.reject}
          </Button>
        </Space>
      ) : null,
    false,
  );

  return (
    <Card title={PM_LABELS.workerTitle}>
      <Space style={{ marginBottom: 16 }}>
        <FilterSelect value={table.filters.status}
          placeholder={PM_LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 220 }}
        />
      </Space>
      <QueryError error={allocations.error} onRetry={() => void allocations.refetch()} />
      <Table
        rowKey="workAllocationId"
        columns={columns}
        dataSource={allocations.data?.items}
        loading={allocations.isFetching}
        pagination={table.pagination(allocations.data?.totalItems)}
        locale={{ emptyText: MESSAGES.common.empty }}
        scroll={{ x: 'max-content' }}
      />
      <ConfirmMovementModal
        movement={confirming}
        onBehalf={false}
        onClose={() => setConfirming(null)}
      />
      <ReasonModal
        open={rejecting !== null}
        title={PM_LABELS.rejectTitle}
        loading={reject.isPending}
        onCancel={() => setRejecting(null)}
        onSubmit={(reason) =>
          rejecting &&
          reject.mutate(
            { id: rejecting.workAllocationId, reason },
            { onSuccess: () => setRejecting(null) },
          )
        }
      />
    </Card>
  );
}
