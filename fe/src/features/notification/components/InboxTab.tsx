import { Button, Checkbox, Space, Tag, type TableColumnsType } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import { useMarkAllRead, useMarkRead, useNotifications } from '../hooks/useNotifications';
import type { NotificationItem } from '../types';
import { DataTable } from '@/components/common/DataTable';

export function InboxTab() {
  const table = useTableQuery<{ unreadOnly?: boolean }>({});
  const list = useNotifications(table.params);
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();

  const columns: TableColumnsType<NotificationItem> = [
    {
      title: 'Thông báo',
      key: 'title',
      render: (_, n) => (
        <Space direction="vertical" size={0}>
          <Space size={4}>
            {n.isUrgent && <Tag color="red">Khẩn</Tag>}
            <strong style={{ fontWeight: n.isRead ? 400 : 600 }}>{n.title}</strong>
          </Space>
          <span>{n.message}</span>
        </Space>
      ),
    },
    { title: 'Lúc', dataIndex: 'createdAt', render: formatDateTime },
    {
      title: MESSAGES.common.actions,
      key: 'action',
      render: (_, n) =>
        n.isRead ? null : (
          <Button type="link" onClick={() => markRead.mutate(n.notificationId)}>
            Đánh dấu đã đọc
          </Button>
        ),
    },
  ];

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <Checkbox onChange={(e) => table.setFilters({ unreadOnly: e.target.checked || undefined })}>
          Chỉ chưa đọc
        </Checkbox>
        <Button onClick={() => markAll.mutate(undefined)}>Đánh dấu tất cả đã đọc</Button>
      </Space>
      <QueryError error={list.error} onRetry={() => void list.refetch()} />
      <DataTable
        rowKey="notificationId"
        columns={columns}
        dataSource={list.data?.items}
        loading={list.isFetching}
        pagination={table.pagination(list.data?.totalItems)}
      />
    </>
  );
}
