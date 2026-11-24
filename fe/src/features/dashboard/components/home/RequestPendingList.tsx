import { Card, Empty, List, Tag, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { REQUEST_TYPE_LABEL, type RequestType } from '@/constants/requestTypes';
import { ROUTES } from '@/constants/routes';
import { formatDateTime } from '@/utils/format';
import { HOME_TEXT } from '../../constants';
import type { HomeRequestItem } from '../../types';

/** Các đơn từ chờ duyệt mới nhất của Quản đốc; bấm mở hộp đơn từ. */
export function RequestPendingList({ items }: { items: HomeRequestItem[] }) {
  const navigate = useNavigate();
  return (
    <Card title={HOME_TEXT.requestsLatest} size="small">
      {items.length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={HOME_TEXT.noRequests} />
      ) : (
        <List
          dataSource={items}
          renderItem={(r) => (
            <List.Item
              style={{ cursor: 'pointer' }}
              onClick={() => navigate(ROUTES.requestInbox)}
              extra={
                <Tag color="blue">
                  {REQUEST_TYPE_LABEL[r.requestType as RequestType] ?? r.requestType}
                </Tag>
              }
            >
              <List.Item.Meta
                title={`${r.requestCode}${r.title ? ` · ${r.title}` : ''}`}
                description={
                  <Typography.Text type="secondary">
                    {[r.teamCode, r.createdByName, formatDateTime(r.createdAt)]
                      .filter(Boolean)
                      .join(' · ')}
                  </Typography.Text>
                }
              />
            </List.Item>
          )}
        />
      )}
    </Card>
  );
}
