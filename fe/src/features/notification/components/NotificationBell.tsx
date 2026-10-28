import { useState } from 'react';
import { BellOutlined } from '@ant-design/icons';
import { Badge, Button, Empty, List, Popover, Space, Tag, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { ROUTES } from '@/constants/routes';
import { formatDateTime } from '@/utils/format';
import {
  useMarkAllRead,
  useMarkRead,
  useNotifications,
  useUnreadCount,
} from '../hooks/useNotifications';
import { useNewNotificationToast } from '../hooks/useNewNotificationToast';
import type { NotificationItem } from '../types';

const LIST_SIZE = 10;

/** UC05: chuông thông báo trên thanh trên cùng — số chưa đọc, 10 thông báo mới nhất, đánh dấu đã đọc. */
export function NotificationBell() {
  const navigate = useNavigate();
  useNewNotificationToast();
  const [open, setOpen] = useState(false);
  const unread = useUnreadCount();
  const list = useNotifications({ page: 1, pageSize: LIST_SIZE }, open);
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();

  const openItem = (n: NotificationItem) => {
    if (!n.isRead) markRead.mutate(n.notificationId);
    if (n.linkUrl) {
      setOpen(false);
      navigate(n.linkUrl);
    }
  };

  const content = (
    <div style={{ width: 360 }}>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 8 }}>
        <Typography.Text strong>Thông báo</Typography.Text>
        <Button
          type="link"
          size="small"
          disabled={(unread.data ?? 0) === 0}
          onClick={() => markAll.mutate(undefined)}
        >
          Đánh dấu tất cả đã đọc
        </Button>
      </Space>
      <List
        size="small"
        loading={list.isFetching}
        locale={{
          emptyText: <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có thông báo" />,
        }}
        dataSource={list.data?.items}
        style={{ maxHeight: 420, overflowY: 'auto' }}
        renderItem={(n) => (
          <List.Item
            onClick={() => openItem(n)}
            style={{
              cursor: 'pointer',
              background: n.isRead ? undefined : 'rgba(22,119,255,0.06)',
            }}
          >
            <List.Item.Meta
              title={
                <Space size={4}>
                  {n.isUrgent && <Tag color="red">Khẩn</Tag>}
                  <Typography.Text strong={!n.isRead}>{n.title}</Typography.Text>
                </Space>
              }
              description={
                <>
                  <div>{n.message}</div>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {formatDateTime(n.createdAt)}
                  </Typography.Text>
                </>
              }
            />
          </List.Item>
        )}
      />
      <Button type="link" block onClick={() => { setOpen(false); navigate(ROUTES.notifications); }}>
        Xem tất cả thông báo
      </Button>
    </div>
  );

  return (
    <Popover
      content={content}
      trigger="click"
      placement="bottomRight"
      open={open}
      onOpenChange={setOpen}
    >
      <Badge count={unread.data ?? 0} size="small" overflowCount={99}>
        <Button
          type="text"
          shape="circle"
          icon={<BellOutlined style={{ fontSize: 18 }} />}
          aria-label="Thông báo"
        />
      </Badge>
    </Popover>
  );
}
