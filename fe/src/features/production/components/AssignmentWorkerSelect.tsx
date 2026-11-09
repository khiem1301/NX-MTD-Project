import { Select } from 'antd';
import { fill } from '@/utils/format';
import { PRODUCTION_TEXT } from '../constants';
import { useAssignOperation, useCloseAssignment } from '../hooks/useAssignments';
import type { AssignmentRow } from '../utils/assignmentBoard';

const text = PRODUCTION_TEXT.assignments;

type AssignmentWorkerSelectProps = {
  orderId: number;
  row: AssignmentRow;
  workerOptions: { value: number; label: string }[];
  workersLoading: boolean;
};

/** Ô chọn công nhân của một công đoạn: chọn thêm là phân công, bỏ chọn là đóng phân công (giữ lịch sử). */
export function AssignmentWorkerSelect({
  orderId,
  row,
  workerOptions,
  workersLoading,
}: AssignmentWorkerSelectProps) {
  const assign = useAssignOperation();
  const close = useCloseAssignment();
  const known = new Set(workerOptions.map((o) => o.value));
  // Công nhân đã nghỉ / chuyển tổ không có trong danh sách của tổ nhưng vẫn phải hiện để tổ trưởng bỏ đi.
  const gone = row.assignments
    .filter((a) => !known.has(a.workerId))
    .map((a) => ({
      value: a.workerId,
      label: fill(text.workerGone, `${a.workerCode} - ${a.workerName}`),
    }));

  return (
    <Select<number[]>
      mode="multiple"
      showSearch
      optionFilterProp="label"
      style={{ width: '100%', minWidth: 260 }}
      placeholder={text.workerPlaceholder}
      notFoundContent={text.noWorkers}
      status={row.isCovered ? undefined : 'warning'}
      aria-label={`${text.columns.worker} ${row.operation.operationCode}`}
      loading={workersLoading || assign.isPending || close.isPending}
      disabled={assign.isPending || close.isPending}
      options={[...workerOptions, ...gone]}
      value={row.assignments.map((a) => a.workerId)}
      onSelect={(workerId: number) =>
        assign.mutate({ orderId, operationId: row.operation.operationId, workerId })
      }
      onDeselect={(workerId: number) => {
        const target = row.assignments.find((a) => a.workerId === workerId);
        if (target) close.mutate(target.operationAssignmentId);
      }}
    />
  );
}
