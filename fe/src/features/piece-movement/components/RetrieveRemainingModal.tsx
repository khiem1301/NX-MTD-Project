import { useState } from 'react';
import { Input, Modal, Select, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { DAILY_REASON_MAX_LENGTH, PM_LABELS } from '../constants';
import { useRetrieveRemaining } from '../hooks/usePieceMovement';
import type { WorkerOption } from '../utils/dailyBoard';

type RetrieveRemainingModalProps = {
  workDate: string;
  workerOptions: WorkerOption[];
  onClose: () => void;
};

/** Tổ trưởng thu hồi phần được giao mà công nhân chưa làm hết trong ngày; bắt buộc lý do. */
export function RetrieveRemainingModal({
  workDate,
  workerOptions,
  onClose,
}: RetrieveRemainingModalProps) {
  const [workerIds, setWorkerIds] = useState<number[]>([]);
  const [reason, setReason] = useState('');
  const retrieve = useRetrieveRemaining();

  return (
    <Modal
      open
      title={PM_LABELS.retrieveTitle}
      okText={PM_LABELS.retrieveRemaining}
      cancelText={MESSAGES.common.cancel}
      okButtonProps={{ disabled: !reason.trim() }}
      confirmLoading={retrieve.isPending}
      onOk={() =>
        retrieve.mutate(
          {
            workDate,
            workerIds: workerIds.length > 0 ? workerIds : undefined,
            reason: reason.trim(),
          },
          { onSuccess: onClose },
        )
      }
      onCancel={onClose}
    >
      <Typography.Paragraph type="secondary">
        {fill(PM_LABELS.retrieveHint, workDate.split('-').reverse().join('/'))}
      </Typography.Paragraph>
      <Select<number[]>
        mode="multiple"
        allowClear
        showSearch
        optionFilterProp="label"
        style={{ width: '100%', marginBottom: 12 }}
        placeholder={PM_LABELS.retrieveWorkers}
        value={workerIds}
        onChange={setWorkerIds}
        options={workerOptions}
      />
      <Input.TextArea
        rows={3}
        maxLength={DAILY_REASON_MAX_LENGTH}
        showCount
        placeholder={PM_LABELS.retrieveReasonPlaceholder}
        aria-label={PM_LABELS.retrieveReason}
        value={reason}
        onChange={(e) => setReason(e.target.value)}
      />
    </Modal>
  );
}
