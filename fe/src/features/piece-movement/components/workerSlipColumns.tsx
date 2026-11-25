import type { TableColumnsType } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { PM_LABELS } from '../constants';
import type { WorkerAllocationSummary } from '../types';
import { negated } from '../utils/signedQty';
import { PendingTags } from './PendingTags';

/** DEV-ALL-182: bảng phiếu của tổ trưởng, một dòng mỗi công nhân; `extra` là nút xác nhận hộ của dòng. */
export function workerSlipColumns(
  extra: (g: WorkerAllocationSummary) => React.ReactNode,
): TableColumnsType<WorkerAllocationSummary> {
  return [
    {
      title: PM_LABELS.worker,
      key: 'worker',
      render: (_, g) => `${g.workerCode} - ${g.workerName}`,
    },
    { title: PM_LABELS.slipCount, key: 'count', align: 'right', render: (_, g) => g.slipCount },
    { title: PM_LABELS.totalHandover, dataIndex: 'handoverQty', align: 'right' },
    {
      title: PM_LABELS.totalRetrieval,
      dataIndex: 'retrievalQty',
      align: 'right',
      render: (v: number) => negated(v),
    },
    {
      title: PM_LABELS.pendingConfirmTitle,
      key: 'pending',
      render: (_, g) => <PendingTags group={g} />,
    },
    { title: MESSAGES.common.actions, key: 'action', render: (_, g) => extra(g) },
  ];
}
