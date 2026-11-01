import { Tag, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import { useSentAnnouncements } from '../hooks/useNotifications';
import type { Announcement } from '../types';
import { DataTable } from '@/components/common/DataTable';
import { SCOPE_LABEL } from '../constants';

export function SentTab() {
  const table = useTableQuery<Record<string, never>>({});
  const list = useSentAnnouncements(table.params);
  const columns: TableColumnsType<Announcement> = [
    { title: 'Nội dung', dataIndex: 'content' },
    {
      title: 'Mức',
      dataIndex: 'priority',
      render: (v: string) => (v === 'URGENT' ? <Tag color="red">Khẩn</Tag> : 'Thường'),
    },
    {
      title: 'Gửi tới',
      key: 'scope',
      render: (_, a) =>
        [SCOPE_LABEL[a.targetScope], ...a.targets]
          .join(': ')
          .replace(': ', a.targets.length ? ': ' : ''),
    },
    { title: 'Người nhận', dataIndex: 'recipientCount', align: 'right' },
    { title: 'Người gửi', dataIndex: 'senderName' },
    { title: 'Lúc', dataIndex: 'sentAt', render: formatDateTime },
  ];
  return (
    <>
      <QueryError error={list.error} onRetry={() => void list.refetch()} />
      <DataTable
        rowKey="announcementId"
        columns={columns}
        dataSource={list.data?.items}
        loading={list.isFetching}
        pagination={table.pagination(list.data?.totalItems)}
      />
    </>
  );
}
