import { CloseCircleOutlined, CheckCircleOutlined } from '@ant-design/icons';
import { useState } from 'react';
import { Button, Card, Space } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { ReasonModal } from '@/components/common/ReasonModal';
import { useTableQuery } from '@/hooks/useTableQuery';
import { fill } from '@/utils/format';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { MAX_BATCH_CONFIRM, MOVEMENT_STATUS, PM_LABELS } from '../constants';
import {
  useAllocations,
  useConfirmBatchByWorker,
  useConfirmByWorker,
  useRejectByWorker,
} from '../hooks/usePieceMovement';
import type { WorkAllocation } from '../types';
import { FilterSelect } from '@/components/common/FilterSelect';
import { STAT_CARD_CLASS } from '@/components/common/cardTable';
import { DataTable } from '@/components/common/DataTable';
import { allocationColumns } from '../components/allocationColumns';
import type { AllocationFilters } from '../types';
import { STATUS_OPTIONS } from '../constants';
import { HintTip } from '@/components/common/HintTip';

const isPending = (m: WorkAllocation) => m.status === MOVEMENT_STATUS.PENDING;

/**
 * UC60: công nhân xem việc được giao / thu hồi của mình rồi tự xác nhận hoặc từ chối.
 * DEV-ALL-126: chọn nhiều phiếu đang chờ để xác nhận một lần.
 */
export function MyPieceMovementsPage() {
  const table = useTableQuery<AllocationFilters>({});
  const allocations = useAllocations(table.params);
  const reject = useRejectByWorker();
  const confirm = useConfirmByWorker();
  const confirmBatch = useConfirmBatchByWorker();
  const [rejecting, setRejecting] = useState<WorkAllocation | null>(null);
  const [selected, setSelected] = useState<WorkAllocation[]>([]);
  const pendingOnPage = (allocations.data?.items ?? []).filter(isPending);

  const columns = allocationColumns(
    (m) =>
      isPending(m) ? (
        <Space size={0} wrap>
          <ConfirmAction
            title={PM_LABELS.confirmTitle}
            description={PM_LABELS.confirmBody}
            okText={PM_LABELS.confirm}
            onConfirm={() => confirm.mutateAsync(m.workAllocationId)}
          >
            <IconAction title={PM_LABELS.confirm} icon={<CheckCircleOutlined />} />
          </ConfirmAction>
          <IconAction
            title={PM_LABELS.reject}
            icon={<CloseCircleOutlined />}
            danger
            onClick={() => setRejecting(m)}
          />
        </Space>
      ) : null,
    false,
  );

  return (
    <Card title={PM_LABELS.workerTitle}>
      <HintTip text={PM_LABELS.workerHint} />
      <Space style={{ marginBottom: 16 }} wrap>
        <FilterSelect
          value={table.filters.status}
          placeholder={PM_LABELS.status}
          options={STATUS_OPTIONS}
          onChange={(status?: string) => table.setFilters({ status })}
          style={{ width: 220 }}
        />
        <Button
          disabled={pendingOnPage.length === 0}
          onClick={() => setSelected(pendingOnPage.slice(0, MAX_BATCH_CONFIRM))}
        >
          {PM_LABELS.selectAllPending}
        </Button>
        <ConfirmAction
          title={fill(PM_LABELS.confirmBatchTitle, selected.length)}
          description={PM_LABELS.confirmBody}
          okText={PM_LABELS.confirm}
          disabled={selected.length === 0}
          onConfirm={() =>
            confirmBatch.mutateAsync(
              { workAllocationIds: selected.map((m) => m.workAllocationId) },
              { onSuccess: () => setSelected([]) },
            )
          }
        >
          <Button type="primary" icon={<CheckCircleOutlined />} disabled={selected.length === 0}>
            {fill(PM_LABELS.confirmSelected, selected.length)}
          </Button>
        </ConfirmAction>
        {selected.length > 0 && (
          <Button type="link" onClick={() => setSelected([])}>
            {PM_LABELS.clearSelection}
          </Button>
        )}
      </Space>
      <QueryError error={allocations.error} onRetry={() => void allocations.refetch()} />
      <DataTable
        className={STAT_CARD_CLASS}
        rowKey="workAllocationId"
        columns={columns}
        dataSource={allocations.data?.items}
        loading={allocations.isFetching}
        pagination={table.pagination(allocations.data?.totalItems)}
        rowSelection={{
          selectedRowKeys: selected.map((m) => m.workAllocationId),
          onChange: (_keys, rows) => setSelected(rows.slice(0, MAX_BATCH_CONFIRM)),
          getCheckboxProps: (m) => ({ disabled: !isPending(m) }),
        }}
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
