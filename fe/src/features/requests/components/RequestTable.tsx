import { Table, type TableColumnsType, type TablePaginationConfig } from 'antd';
import { StatusTag } from '@/components/common/StatusTag';
import { MESSAGES } from '@/constants/messages';
import { REQUEST_TYPE_LABEL } from '@/constants/requestTypes';
import type { Paginated } from '@/types/api';
import { formatDate, formatDateTime } from '@/utils/format';
import { REQUEST_STATUS_TAG } from '../constants';
import type { RequestListItem } from '../types';

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
      render: (v: string, r) => <a onClick={() => onOpen(r.requestId)}>{v}</a>,
    },
    { title: MESSAGES.requests.type, dataIndex: 'requestType', render: (v: keyof typeof REQUEST_TYPE_LABEL) => REQUEST_TYPE_LABEL[v] ?? v },
    { title: MESSAGES.requests.titleField, dataIndex: 'title', ellipsis: true, render: (v: string | null) => v ?? '—' },
    ...(showTeam
      ? [{ title: MESSAGES.requests.team, dataIndex: 'teamName', render: (v: string | null) => v ?? '—' }]
      : []),
    { title: MESSAGES.requests.createdBy, dataIndex: 'createdByName', render: (v: string | null) => v ?? '—' },
    { title: MESSAGES.requests.createdAt, dataIndex: 'createdAt', render: formatDateTime },
    { title: MESSAGES.requests.targetDate, dataIndex: 'targetDate', render: formatDate },
    {
      title: MESSAGES.requests.status,
      dataIndex: 'status',
      render: (v: string) => <StatusTag status={v} labels={REQUEST_STATUS_TAG} />,
    },
  ];

  return (
    <Table<RequestListItem>
      rowKey="requestId"
      size="middle"
      loading={loading}
      columns={columns}
      dataSource={data?.items ?? []}
      pagination={pagination}
      locale={{ emptyText: MESSAGES.common.noData }}
    />
  );
}
