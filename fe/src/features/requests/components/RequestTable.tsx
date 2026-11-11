import { IconAction } from '@/components/common/IconAction';
import { EyeOutlined } from '@ant-design/icons';
import { Tag, Typography, type TableColumnsType, type TablePaginationConfig } from 'antd';
import dayjs from 'dayjs';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { REQUEST_TYPE_LABEL } from '@/constants/requestTypes';
import type { Paginated } from '@/types/api';
import { formatDate, formatDateTime } from '@/utils/format';
import { OPEN_STATUSES, REQUEST_STATUS_TAG } from '../constants';
import type { RequestListItem } from '@/types/request';
import { DataTable } from '@/components/common/DataTable';

type RequestTableProps = {
  data?: Paginated<RequestListItem>;
  loading: boolean;
  pagination: TablePaginationConfig;
  showTeam: boolean;
  onOpen: (requestId: number) => void;
};

export function RequestTable({ data, loading, pagination, showTeam, onOpen }: RequestTableProps) {
  const columns: TableColumnsType<RequestListItem> = [
    {
      title: MESSAGES.requests.code,
      dataIndex: 'requestCode',
    },
    {
      title: MESSAGES.requests.type,
      dataIndex: 'requestType',
      render: (v: keyof typeof REQUEST_TYPE_LABEL) => REQUEST_TYPE_LABEL[v] ?? v,
    },
    {
      title: MESSAGES.requests.titleField,
      dataIndex: 'title',
      ellipsis: true,
      render: (v: string | null) => v ?? '—',
    },
    ...(showTeam
      ? [
          {
            title: MESSAGES.requests.team,
            dataIndex: 'teamName',
            render: (v: string | null) => v ?? '—',
          },
        ]
      : []),
    {
      title: MESSAGES.requests.createdBy,
      dataIndex: 'createdByName',
      render: (v: string | null) => v ?? '—',
    },
    { title: MESSAGES.requests.createdAt, dataIndex: 'createdAt', render: formatDateTime },
    {
      title: MESSAGES.requests.targetDate,
      dataIndex: 'targetDate',
      render: (v: string | null, r) =>
        isOverdue(r) ? (
          <Typography.Text type="danger">
            {formatDate(v)} <Tag color="red">{MESSAGES.requests.overdue}</Tag>
          </Typography.Text>
        ) : (
          formatDate(v)
        ),
    },
    {
      title: MESSAGES.requests.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={REQUEST_STATUS_TAG} />,
    },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, r) => (
        <IconAction
          title={MESSAGES.common.view}
          icon={<EyeOutlined />}
          onClick={(e) => {
            e?.stopPropagation();
            onOpen(r.requestId);
          }}
        />
      ),
    },
  ];

  return (
    <DataTable<RequestListItem>
      rowKey="requestId"
      size="small"
      loading={loading}
      columns={columns}
      dataSource={data?.items ?? []}
      pagination={pagination}
      onRow={(r) => ({ onClick: () => onOpen(r.requestId), style: { cursor: 'pointer' } })}
    />
  );
}

/** Đơn còn mở mà ngày cần đã qua. */
const isOverdue = (r: RequestListItem) =>
  r.targetDate !== null &&
  OPEN_STATUSES.includes(r.status) &&
  dayjs(r.targetDate).isBefore(dayjs(), 'day');
