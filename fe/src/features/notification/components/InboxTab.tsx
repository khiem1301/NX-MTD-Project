import { CheckOutlined } from '@ant-design/icons';
import { Button, Checkbox, Segmented, Space, Tag, type TableColumnsType } from 'antd';
import { IconAction } from '@/components/common/IconAction';
import { QueryError } from '@/components/common/QueryError';
import { MESSAGES } from '@/constants/messages';
import { useTableQuery } from '@/hooks/useTableQuery';
import { formatDateTime } from '@/utils/format';
import { useMarkAllRead, useMarkRead, useNotifications } from '../hooks/useNotifications';
import type { NotificationGroup, NotificationItem } from '../types';
import { DataTable } from '@/components/common/DataTable';

export function InboxTab() {
  const table = useTableQuery<{ unreadOnly?: boolean; group?: NotificationGroup }>({});
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
            <Tag color={n.isManual ? 'blue' : 'default'}>{n.isManual ? 'Từ quản lý' : 'Hệ thống'}</Tag>
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
          <IconAction
            title="Đánh dấu đã đọc"
            icon={<CheckOutlined />}
            onClick={() => markRead.mutate(n.notificationId)}
          />
        ),
    },
  ];

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <Segmented
          value={table.params.group ?? 'ALL'}
          options={[
            { value: 'ALL', label: 'Tất cả' },
            { value: 'MANUAL', label: 'Từ quản lý' },
            { value: 'AUTO', label: 'Hệ thống' },
          ]}
          onChange={(v) => table.setFilters({ group: v === 'ALL' ? undefined : (v as NotificationGroup) })}
        />
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
