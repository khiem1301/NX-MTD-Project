import { Card, Empty, Space, Tag, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import {
  REQUEST_STATUS_LABEL,
  REQUEST_TYPE_LABEL,
  type RequestStatus,
  type RequestType,
} from '@/constants/requestTypes';
import { ROUTES } from '@/constants/routes';
import { formatDateTime } from '@/utils/format';
import { HOME_ROLE_TEXT as T } from '../../constants';
import { useElectricianHome } from '../../hooks/useHome';
import { HomeState } from './HomeState';
import { SectionLabel } from './SectionLabel';

/** Trang chủ Cơ điện: các đơn từ do mình tạo còn mở, hiện như ticket (chỉ xem). */
export function ElectricianHome() {
  const query = useElectricianHome();
  const navigate = useNavigate();
  return (
    <HomeState query={query}>
      {(d) => (
        <>
          <SectionLabel>{T.cards.tickets}</SectionLabel>
          <Card size="small">
            <Typography.Paragraph type="secondary" style={{ marginTop: 0 }}>
              {T.ticketsHint}
            </Typography.Paragraph>
            {d.tickets.length === 0 ? (
              <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={T.noTickets} />
            ) : (
              <Space direction="vertical" style={{ width: '100%' }}>
                {d.tickets.map((t) => (
                  <Card
                    key={t.requestId}
                    size="small"
                    hoverable
                    onClick={() => navigate(ROUTES.requests)}
                  >
                    <Space style={{ justifyContent: 'space-between', width: '100%' }} align="start">
                      <div>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {t.code} ·{' '}
                          {REQUEST_TYPE_LABEL[t.requestType as RequestType] ?? t.requestType}
                        </Typography.Text>
                        <div>
                          <Typography.Text strong>{t.title ?? t.code}</Typography.Text>
                        </div>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {formatDateTime(t.createdAt)}
                        </Typography.Text>
                      </div>
                      <Tag color="blue">
                        {REQUEST_STATUS_LABEL[t.status as RequestStatus] ?? t.status}
                      </Tag>
                    </Space>
                  </Card>
                ))}
              </Space>
            )}
          </Card>
        </>
      )}
    </HomeState>
  );
}
