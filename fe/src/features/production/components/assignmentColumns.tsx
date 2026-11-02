import { Space, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { PRODUCTION_TEXT } from '../constants';
import type { AssignmentRow } from '../utils/assignmentBoard';
import { AssignmentWorkerSelect } from './AssignmentWorkerSelect';

const text = PRODUCTION_TEXT.assignments;

type Workers = { options: { value: number; label: string }[]; loading: boolean };

/** Cột của bảng phân công: công đoạn, bậc, công nhân (chọn ngay trên dòng). */
export const assignmentColumns = (
  orderId: number,
  workers: Workers,
): ColumnsType<AssignmentRow> => [
  {
    title: text.columns.operation,
    key: 'op',
    render: (_, { operation: o }) => (
      <Space direction="vertical" size={0}>
        <Typography.Text strong>{`${o.operationCode} - ${o.operationName}`}</Typography.Text>
        <Typography.Text type="secondary">{o.groupName}</Typography.Text>
      </Space>
    ),
  },
  {
    title: text.columns.skill,
    key: 'skill',
    width: 70,
    align: 'center',
    render: (_, { operation: o }) => o.skillLevel ?? '—',
  },
  {
    title: text.columns.worker,
    key: 'worker',
    width: '55%',
    render: (_, row) => (
      <AssignmentWorkerSelect
        orderId={orderId}
        row={row}
        workerOptions={workers.options}
        workersLoading={workers.loading}
      />
    ),
  },
];
