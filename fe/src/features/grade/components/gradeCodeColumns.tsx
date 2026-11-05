import {
  DeleteOutlined,
  DollarOutlined,
  EditOutlined,
  StopOutlined,
  PlayCircleOutlined,
  EyeOutlined,
} from '@ant-design/icons';
import { Space, Tag, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { ConfirmAction } from '@/components/common/ConfirmAction';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate } from '@/utils/format';
import { GR_LABELS } from '../constants';
import type { GradeCode, GradeLevel } from '../types';
import { currentLevel, upcomingLevels } from '../utils/gradeLevels';
import { UpcomingLevelsCell } from './UpcomingLevelsCell';

type GradeCodeColumnsOptions = {
  levels: GradeLevel[];
  onView: (row: GradeCode) => void;
  onChangeAmount: (row: GradeCode) => void;
  onEdit: (row: GradeCode) => void;
  onToggleStatus: (row: GradeCode) => void;
  onRemove: (row: GradeCode) => void;
};

/** Cột bảng "Bậc & mức tiền": mỗi dòng một bậc kèm mức tiền đang áp dụng và các mức sắp áp dụng. */
export const gradeCodeColumns = ({
  levels,
  onView,
  onChangeAmount,
  onEdit,
  onToggleStatus,
  onRemove,
}: GradeCodeColumnsOptions): TableColumnsType<GradeCode> => [
  {
    title: GR_LABELS.gradeCode,
    dataIndex: 'gradeCode',
    width: 100,
  },
  { title: GR_LABELS.gradeName, dataIndex: 'gradeName' },
  {
    title: GR_LABELS.amount,
    key: 'amount',
    align: 'right',
    render: (_, row) => {
      const cur = currentLevel(levels, row.gradeCode);
      return cur ? formatCurrency(cur.monthlyAmount) : <Tag>{GR_LABELS.none}</Tag>;
    },
  },
  {
    title: GR_LABELS.effectiveDate,
    key: 'effective',
    render: (_, row) => {
      const cur = currentLevel(levels, row.gradeCode);
      return (
        <Space direction="vertical" size={0}>
          {cur ? formatDate(cur.effectiveDate) : '—'}
          <UpcomingLevelsCell levels={upcomingLevels(levels, row.gradeCode)} />
        </Space>
      );
    },
  },
  {
    title: GR_LABELS.status,
    dataIndex: 'status',
    render: (v: GradeCode['status'], row) => (
      <Space size={4} wrap>
        <Tag color={v === 'Active' ? 'green' : 'default'}>
          {v === 'Active' ? GR_LABELS.active : GR_LABELS.inactive}
        </Tag>
        {row.inUse && <Tag color="blue">{GR_LABELS.inUse}</Tag>}
      </Space>
    ),
  },
  {
    title: MESSAGES.common.actions,
    key: 'action',
    render: (_, row) => (
      <Space size={0} wrap>
        <IconAction
          title={MESSAGES.common.view}
          icon={<EyeOutlined />}
          onClick={() => onView(row)}
        />
        <IconAction
          title={GR_LABELS.changeAmount}
          icon={<DollarOutlined />}
          onClick={() => onChangeAmount(row)}
        />
        <IconAction
          title={MESSAGES.common.edit}
          icon={<EditOutlined />}
          onClick={() => onEdit(row)}
        />
        <IconAction
          title={row.status === 'Active' ? GR_LABELS.deactivate : GR_LABELS.reactivate}
          icon={row.status === 'Active' ? <StopOutlined /> : <PlayCircleOutlined />}
          onClick={() => onToggleStatus(row)}
        />
        {!row.inUse && (
          <ConfirmAction title={GR_LABELS.deleteConfirm} onConfirm={() => onRemove(row)}>
            <IconAction title={MESSAGES.common.delete} danger icon={<DeleteOutlined />} />
          </ConfirmAction>
        )}
      </Space>
    ),
  },
];
