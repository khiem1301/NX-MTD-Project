import { DeleteOutlined, DollarOutlined, EditOutlined } from '@ant-design/icons';
import { Button, Popconfirm, Space, Tag, type TableColumnsType } from 'antd';
import { DetailLink } from '@/components/common/DetailLink';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatDate } from '@/utils/format';
import { GR_LABELS } from '../constants';
import type { GradeCode, GradeLevel } from '../types';
import { currentLevel, upcomingLevels } from '../utils';
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
    render: (v: string, row) => <DetailLink onClick={() => onView(row)}>{v}</DetailLink>,
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
        <Button type="link" icon={<DollarOutlined />} onClick={() => onChangeAmount(row)}>
          {GR_LABELS.changeAmount}
        </Button>
        <Button type="link" icon={<EditOutlined />} onClick={() => onEdit(row)}>
          {MESSAGES.common.edit}
        </Button>
        <Button type="link" onClick={() => onToggleStatus(row)}>
          {row.status === 'Active' ? GR_LABELS.deactivate : GR_LABELS.reactivate}
        </Button>
        {!row.inUse && (
          <Popconfirm title={GR_LABELS.deleteConfirm} onConfirm={() => onRemove(row)}>
            <Button type="link" danger icon={<DeleteOutlined />}>
              {MESSAGES.common.delete}
            </Button>
          </Popconfirm>
        )}
      </Space>
    ),
  },
];
