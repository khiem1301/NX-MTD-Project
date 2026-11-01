import { BellOutlined } from '@ant-design/icons';
import { Button, Card, List, Tag, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { ROUTES } from '@/constants/routes';
import { useMarkRead, useNotifications } from '../hooks/useNotifications';
import { formatDateTime } from '@/utils/format';

const LIMIT = 3;

/** Trang chủ mọi vai trò: các thông báo chưa đọc mới nhất (nghỉ lễ, sự kiện do Quản đốc gửi...). */
export function LatestNotificationsCard() {
  const navigate = useNavigate();
  const list = useNotifications({ page: 1, pageSize: LIMIT, unreadOnly: true });
  const markRead = useMarkRead();
  const items = list.data?.items ?? [];

  return (
    <Card
      title={
        <>
          <BellOutlined /> Thông báo mới
        </>
      }
      style={{ marginTop: 16 }}
      loading={list.isLoading}
      extra={
        <Button type="link" onClick={() => navigate(ROUTES.notifications)}>
          Xem tất cả
        </Button>
      }
    >
      <List
        locale={{ emptyText: 'Không có thông báo mới.' }}
        dataSource={items}
        renderItem={(n) => (
          <List.Item
            actions={[
              <Button key="read" type="link" onClick={() => markRead.mutate(n.notificationId)}>
                Đã đọc
              </Button>,
            ]}
          >
            <List.Item.Meta
              title={
                <>
                  {n.isUrgent && <Tag color="red">Khẩn</Tag>}
                  {n.title}
                </>
              }
              description={
                <>
                  <Typography.Paragraph style={{ marginBottom: 0 }}>
                    {n.message}
                  </Typography.Paragraph>
                  <Typography.Text type="secondary">{formatDateTime(n.createdAt)}</Typography.Text>
                </>
              }
            />
          </List.Item>
        )}
      />
    </Card>
  );
}
