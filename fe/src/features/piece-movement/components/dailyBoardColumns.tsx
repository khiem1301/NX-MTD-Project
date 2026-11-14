import { EditOutlined } from '@ant-design/icons';
import { Tag, Typography, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { PM_LABELS } from '../constants';
import type { DailyWorker } from '../utils/dailyBoard';

/** Cột bảng công nhân của màn giao theo ngày; số giao trong ngày kèm phần còn chờ công nhân xác nhận. */
export const dailyBoardColumns = (
  onEdit: (workerId: number) => void,
): TableColumnsType<DailyWorker> => [
  {
    title: PM_LABELS.worker,
    key: 'worker',
    render: (_, w) => <Typography.Text strong>{w.label}</Typography.Text>,
  },
  {
    title: PM_LABELS.operationCount,
    key: 'operationCount',
    width: 130,
    align: 'center',
    render: (_, w) => (
      <Tag color={w.operationCount === 0 ? 'orange' : 'default'} style={{ marginInlineEnd: 0 }}>
        {w.operationCount}
      </Tag>
    ),
  },
  { title: PM_LABELS.carried, dataIndex: 'carriedQty', width: 140, align: 'right' },
  {
    title: PM_LABELS.allocated,
    key: 'allocated',
    width: 190,
    align: 'right',
    render: (_, w) => (
      <>
        {w.allocatedQty}
        {w.pendingQty > 0 && (
          <Tag color="gold" style={{ marginInlineStart: 8, marginInlineEnd: 0 }}>
            {fill(PM_LABELS.pendingConfirm, w.pendingQty)}
          </Tag>
        )}
      </>
    ),
  },
  { title: PM_LABELS.output, dataIndex: 'outputQty', width: 100, align: 'right' },
  { title: PM_LABELS.remaining, dataIndex: 'remainingQty', width: 130, align: 'right' },
  {
    title: MESSAGES.common.actions,
    key: 'actions',
    render: (_, w) => (
      <IconAction
        title={PM_LABELS.giveWork}
        icon={<EditOutlined />}
        onClick={() => onEdit(w.workerId)}
      />
    ),
  },
];
