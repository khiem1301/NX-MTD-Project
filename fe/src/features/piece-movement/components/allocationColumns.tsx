import { Space, Tag, Typography, type TableColumnsType } from 'antd';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { formatDateTime } from '@/utils/format';
import {
  CONFIRM_MODE_LABEL,
  MOVEMENT_STATUS,
  MOVEMENT_STATUS_LABEL,
  MOVEMENT_TYPE_LABEL,
  PM_LABELS,
  RETRIEVAL_REASON_LABEL,
} from '../constants';
import type { WorkAllocation } from '../types';

export function allocationColumns(
  extra: (m: WorkAllocation) => React.ReactNode,
  withWorker: boolean,
): TableColumnsType<WorkAllocation> {
  return [
    {
      title: PM_LABELS.type,
      dataIndex: 'movementType',
      render: (v: string, m) => (
        <Space direction="vertical" size={0}>
          <Tag color={v === 'HANDOVER' ? 'blue' : 'orange'}>{MOVEMENT_TYPE_LABEL[v]}</Tag>
          {m.retrievalReason && (
            <Typography.Text type="secondary">
              {RETRIEVAL_REASON_LABEL[m.retrievalReason]}
            </Typography.Text>
          )}
        </Space>
      ),
    },
    ...(withWorker
      ? [
          {
            title: PM_LABELS.worker,
            key: 'worker',
            render: (_: unknown, m: WorkAllocation) => `${m.workerCode} - ${m.workerName}`,
          },
        ]
      : []),
    {
      title: PM_LABELS.orderLine,
      key: 'line',
      render: (_, m) => `${m.orderCode} · ${m.productCode} ${m.color}-${m.sizeCode}`,
    },
    {
      title: PM_LABELS.operation,
      key: 'operation',
      render: (_, m) => `${m.operationCode} - ${m.operationName}`,
    },
    { title: PM_LABELS.quantity, dataIndex: 'quantity', align: 'right' },
    {
      title: PM_LABELS.confirmedQty,
      dataIndex: 'confirmedQty',
      align: 'right',
      render: (v: number | null) => v ?? '—',
    },
    {
      title: PM_LABELS.status,
      dataIndex: 'status',
      render: (v: string, m) => (
        <Space direction="vertical" size={0}>
          <StatusTag status={v} labels={MOVEMENT_STATUS_LABEL} />
          {v === MOVEMENT_STATUS.CONFIRMED && (
            <Typography.Text type="secondary">{CONFIRM_MODE_LABEL[m.confirmMode]}</Typography.Text>
          )}
          {m.rejectReason && <Typography.Text type="danger">{m.rejectReason}</Typography.Text>}
        </Space>
      ),
    },
    { title: PM_LABELS.createdAt, dataIndex: 'createdAt', render: formatDateTime },
    { title: MESSAGES.common.actions, key: 'action', render: (_, m) => extra(m) },
  ];
}
