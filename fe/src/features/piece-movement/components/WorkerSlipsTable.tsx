import { useState } from 'react';
import { Table } from 'antd';
import { CARD_TABLE_CLASS, withCardLabels } from '@/components/common/cardTable';
import { QueryError } from '@/components/common/QueryError';
import { useAllocations } from '../hooks/usePieceMovement';
import type { AllocationFilters } from '../types';
import { allocationColumns } from './allocationColumns';

const SLIP_PAGE_SIZE = 10;
/** Cột của phiếu trong dòng mở rộng: chỉ xem, không có thao tác (xác nhận hộ làm ở dòng công nhân). */
const columns = allocationColumns(() => null, false).filter((c) => c.key !== 'action');

type Props = { workerId: number; filters: AllocationFilters };

/** Các phiếu của một công nhân (cùng bộ lọc loại / trạng thái của tab), chỉ tải khi mở dòng. */
export function WorkerSlipsTable({ workerId, filters }: Props) {
  const [page, setPage] = useState(1);
  const slips = useAllocations({ ...filters, workerId, page, pageSize: SLIP_PAGE_SIZE });
  return (
    <>
      <QueryError error={slips.error} onRetry={() => void slips.refetch()} />
      <Table
        size="small"
        rowKey="workAllocationId"
        className={CARD_TABLE_CLASS}
        columns={withCardLabels(columns)}
        dataSource={slips.data?.items}
        loading={slips.isFetching}
        pagination={{
          current: page,
          pageSize: SLIP_PAGE_SIZE,
          total: slips.data?.totalItems ?? 0,
          hideOnSinglePage: true,
          showSizeChanger: false,
          onChange: setPage,
        }}
      />
    </>
  );
}
