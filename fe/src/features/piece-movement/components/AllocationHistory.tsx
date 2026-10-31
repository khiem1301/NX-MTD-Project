import { useState } from 'react';
import { Button } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { useTableQuery } from '@/hooks/useTableQuery';
import { ConfirmMovementModal } from './ConfirmMovementModal';
import { MOVEMENT_STATUS, PM_LABELS } from '../constants';
import { useAllocations } from '../hooks/usePieceMovement';
import type { WorkAllocation } from '../types';
import { FilterSelect } from '@/components/common/FilterSelect';
import { DataTable } from '@/components/common/DataTable';
import { TableToolbar } from '@/components/common/TableToolbar';
import type { AllocationFilters } from '../types';
import { allocationColumns } from './allocationColumns';
import { STATUS_OPTIONS, TYPE_OPTIONS } from '../constants';

export function AllocationHistory() {
  const table = useTableQuery<AllocationFilters>({});
  const allocations = useAllocations(table.params);
  const [onBehalf, setOnBehalf] = useState<WorkAllocation | null>(null);

  const columns = allocationColumns(
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
      <QueryError error={allocations.error} onRetry={() => void allocations.refetch()} />
      <DataTable
        rowKey="workAllocationId"
        columns={columns}
        dataSource={allocations.data?.items}
        loading={allocations.isFetching}
        pagination={table.pagination(allocations.data?.totalItems)}
      />
      <ConfirmMovementModal movement={onBehalf} onBehalf onClose={() => setOnBehalf(null)} />
    </>
  );
}
