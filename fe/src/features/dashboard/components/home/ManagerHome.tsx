import { Col, Row, Spin, Typography } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import { ROUTES } from '@/constants/routes';
import { HOME_TEXT } from '../../constants';
import { useManagerHome } from '../../hooks/useHome';
import { OrderProgressGrid } from './OrderProgressGrid';
import { RequestPendingList } from './RequestPendingList';
import { SectionLabel } from './SectionLabel';
import { StatTile } from './StatTile';

/** Trang chủ Quản đốc: việc chờ xử lý, tiến độ đơn còn mở, đơn từ chờ duyệt. */
export function ManagerHome() {
  const home = useManagerHome();
  if (home.isPending) return <Spin />;
  if (home.isError) return <QueryError error={home.error} onRetry={() => void home.refetch()} />;
  const d = home.data;
  const absent = d.leaderAbsentTeams;
  return (
    <>
      <SectionLabel>{HOME_TEXT.pending}</SectionLabel>
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        <Col xs={24} md={8}>
          <StatTile
            label={HOME_TEXT.receipts}
            value={d.receiptsPending}
            tone={d.receiptsPending > 0 ? 'warn' : 'default'}
            to={ROUTES.materialReceipts}
          />
        </Col>
        <Col xs={24} md={8}>
          <StatTile
            label={HOME_TEXT.requests}
            value={d.requestsPending}
            tone={d.requestsPending > 0 ? 'danger' : 'default'}
            to={ROUTES.requestInbox}
          />
        </Col>
        <Col xs={24} md={8}>
          <StatTile
            label={HOME_TEXT.leaderAbsent}
            value={absent.length}
            tone={absent.length > 0 ? 'warn' : 'ok'}
            note={
              <Typography.Text type="secondary">
                {absent.length > 0 ? absent.join(', ') : HOME_TEXT.leaderAbsentNone}
              </Typography.Text>
            }
          />
        </Col>
      </Row>
      <OrderProgressGrid title={HOME_TEXT.progressAll} orders={d.orders} />
      <div style={{ marginTop: 16 }}>
        <RequestPendingList items={d.requests} />
      </div>
    </>
  );
}
