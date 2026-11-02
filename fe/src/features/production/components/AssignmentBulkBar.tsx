import { useState } from 'react';
import { Alert, Button, Select, Space, Typography, message } from 'antd';
import { fill } from '@/utils/format';
import { ASSIGNMENT_MAX_OPERATIONS, PRODUCTION_TEXT } from '../constants';
import { useAssignOperations } from '../hooks/useProduction';

const text = PRODUCTION_TEXT.assignments;

type AssignmentBulkBarProps = {
  orderId: number;
  operationIds: number[];
  workerOptions: { value: number; label: string }[];
  onDone: () => void;
};

/** Hiện khi đã tích chọn công đoạn, dính ở cuối màn hình để luôn thấy khi cuộn bảng dài: giao tất cả công đoạn đã chọn cho một công nhân, người đang làm công đoạn đó vẫn giữ nguyên. */
export function AssignmentBulkBar({
  orderId,
  operationIds,
  workerOptions,
  onDone,
}: AssignmentBulkBarProps) {
  const [workerId, setWorkerId] = useState<number>();
  const assign = useAssignOperations();
  const tooMany = operationIds.length > ASSIGNMENT_MAX_OPERATIONS;

  const apply = () => {
    if (workerId === undefined) return;
    assign.mutate(
      { orderId, workerId, operationIds },
      {
        onSuccess: (added) => {
          const skipped = operationIds.length - added;
          if (added === 0) void message.info(text.done.noneAdded);
          else if (skipped > 0) void message.success(fill(text.done.addedSkipped, added, skipped));
          else void message.success(fill(text.done.added, added));
          setWorkerId(undefined);
          onDone();
        },
      },
    );
  };

  return (
    <Alert
      type="info"
      style={{
        position: 'sticky',
        bottom: 16,
        zIndex: 3,
        marginTop: 12,
        boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)',
      }}
      message={
        <Space wrap>
          {fill(text.selectedCount, operationIds.length)}
          <Select<number>
            showSearch
            optionFilterProp="label"
            style={{ width: 240 }}
            placeholder={text.bulkWorker}
            aria-label={text.bulkWorker}
            notFoundContent={text.noWorkers}
            options={workerOptions}
            value={workerId}
            onChange={setWorkerId}
          />
          <Button
            type="primary"
            disabled={workerId === undefined || tooMany}
            loading={assign.isPending}
            onClick={apply}
          >
            {text.bulkApply}
          </Button>
          <Button disabled={assign.isPending} onClick={onDone}>
            {text.bulkClear}
          </Button>
          {tooMany && (
            <Typography.Text type="danger">
              {fill(text.tooManySelected, ASSIGNMENT_MAX_OPERATIONS)}
            </Typography.Text>
          )}
        </Space>
      }
    />
  );
}
