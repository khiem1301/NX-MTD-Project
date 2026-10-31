import { useState } from 'react';
import { Button, Card, Space } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { useTableQuery } from '@/hooks/useTableQuery';
import { ConfirmMovementModal } from '../components/ConfirmMovementModal';
import { MOVEMENT_STATUS, PM_LABELS } from '../constants';
import { useAllocations, useRejectByWorker } from '../hooks/usePieceMovement';
import type { WorkAllocation } from '../types';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';
import { allocationColumns } from '../components/allocationColumns';
import type { AllocationFilters } from '../types';
import { STATUS_OPTIONS } from '../constants';

/** UC60: công nhân xem việc được giao / thu hồi của mình rồi tự xác nhận hoặc từ chối. */
export function MyPieceMovementsPage() {
  const table = useTableQuery<AllocationFilters>({});
  const allocations = useAllocations(table.params);
  const reject = useRejectByWorker();
  const [confirming, setConfirming] = useState<WorkAllocation | null>(null);
  const [rejecting, setRejecting] = useState<WorkAllocation | null>(null);

  const columns = allocationColumns(
    (m) =>
      m.status === MOVEMENT_STATUS.PENDING ? (
        <Space size={0} wrap>
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
        <FilterSelect
          value={table.filters.status}
          placeholder={PM_LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 220 }}
        />
      </Space>
      <QueryError error={allocations.error} onRetry={() => void allocations.refetch()} />
      <DataTable
        rowKey="workAllocationId"
        columns={columns}
        dataSource={allocations.data?.items}
        loading={allocations.isFetching}
        pagination={table.pagination(allocations.data?.totalItems)}
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
