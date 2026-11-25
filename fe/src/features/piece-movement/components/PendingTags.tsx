import { Space, Tag, Typography } from 'antd';
import { fill } from '@/utils/format';
import { PM_LABELS } from '../constants';
import type { WorkerAllocationSummary } from '../types';
import { negated } from '../utils/signedQty';

/** Phần "Chờ CN xác nhận": số phiếu, số lần giao và số lần thu hồi còn chờ, mỗi loại một thẻ. */
export function PendingTags({ group }: { group: WorkerAllocationSummary }) {
  if (group.pendingCount === 0) return <Typography.Text type="secondary">—</Typography.Text>;
  return (
    <Space size={4} wrap>
      <Tag color="gold">{fill(PM_LABELS.pendingSlips, group.pendingCount)}</Tag>
      {group.pendingHandoverQty > 0 && (
        <Tag color="blue">{fill(PM_LABELS.pendingHandover, group.pendingHandoverQty)}</Tag>
      )}
      {group.pendingRetrievalQty > 0 && (
        <Tag color="orange">
          {fill(PM_LABELS.pendingRetrieval, negated(group.pendingRetrievalQty))}
        </Tag>
      )}
    </Space>
  );
}
