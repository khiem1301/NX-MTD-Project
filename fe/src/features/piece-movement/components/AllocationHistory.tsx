import { UserSwitchOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { useTableQuery } from '@/hooks/useTableQuery';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';
import { PM_LABELS, STATUS_OPTIONS, TYPE_OPTIONS } from '../constants';
import { useWorkerSummaries } from '../hooks/usePieceMovement';
import type { AllocationFilters, WorkerAllocationSummary } from '../types';
import { ConfirmWorkerOnBehalfModal } from './ConfirmWorkerOnBehalfModal';
import { WorkerSlipsTable } from './WorkerSlipsTable';
import { workerSlipColumns } from './workerSlipColumns';

/**
 * UC53 / UC54 (DEV-ALL-182): phiếu giao / thu hồi gộp theo công nhân. BE gộp và phân trang theo công nhân, mỗi dòng
 * kèm số chờ xác nhận (giao và thu hồi); xác nhận hộ một lần cho mọi phiếu đang chờ ở modal.
 */
export function AllocationHistory() {
  const table = useTableQuery<AllocationFilters>({});
  const workers = useWorkerSummaries(table.params);
  const [onBehalf, setOnBehalf] = useState<WorkerAllocationSummary | null>(null);

  const columns = workerSlipColumns((g) =>
    g.pendingCount > 0 ? (
      <IconAction
        title={PM_LABELS.confirmWorkerOnBehalf}
        icon={<UserSwitchOutlined />}
        onClick={() => setOnBehalf(g)}
      />
    ) : null,
  );

  return (
    <>
      <TableToolbar
        filters={
          <>
            <FilterSelect
              value={table.filters.movementType}
              placeholder={PM_LABELS.type}
              options={TYPE_OPTIONS}
              onChange={(movementType?: string) => table.setFilters({ movementType })}
              style={{ width: 170 }}
            />
            <FilterSelect
              value={table.filters.status}
              placeholder={PM_LABELS.status}
              options={STATUS_OPTIONS}
              onChange={(status?: string) => table.setFilters({ status })}
              style={{ width: 220 }}
            />
          </>
        }
      />
      <QueryError error={workers.error} onRetry={() => void workers.refetch()} />
      <DataTable
        rowKey="workerId"
        columns={columns}
        dataSource={workers.data?.items}
        loading={workers.isFetching}
        pagination={table.pagination(workers.data?.totalItems)}
        expandable={{
          expandedRowRender: (g) => (
            <WorkerSlipsTable workerId={g.workerId} filters={table.filters} />
          ),
        }}
      />
      <ConfirmWorkerOnBehalfModal group={onBehalf} onClose={() => setOnBehalf(null)} />
    </>
  );
}
