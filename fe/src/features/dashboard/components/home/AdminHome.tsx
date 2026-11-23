import { Col, Row, Spin, Typography } from 'antd';
import { QueryError } from '@/components/common/QueryError';
import {
  COST_SUMMARY_ROUTES,
  PAYROLL_ROUTES,
  PRODUCTION_ROUTES,
  REVENUE_ROUTES,
  ROUTES,
} from '@/constants/routes';
import { HOME_TEXT } from '../../constants';
import { useAdminHome } from '../../hooks/useHome';
import { MoneyTile } from './MoneyTile';
import { OrderProgressGrid } from './OrderProgressGrid';
import { SectionLabel } from './SectionLabel';
import { StatTile } from './StatTile';

/** Trang chủ Giám đốc: việc cần quyết, số liệu tháng, tiến độ các đơn đang sản xuất / giao dở. */
export function AdminHome() {
  const home = useAdminHome();
  if (home.isPending) return <Spin />;
  if (home.isError) return <QueryError error={home.error} onRetry={() => void home.refetch()} />;
  const d = home.data;
  return (
    <>
      <SectionLabel>{HOME_TEXT.decide}</SectionLabel>
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12}>
          <StatTile
            label={HOME_TEXT.pieceRates}
            value={d.pieceRatesPending}
            tone={d.pieceRatesPending > 0 ? 'warn' : 'default'}
            to={PRODUCTION_ROUTES.adminPieceRates}
          />
        </Col>
        <Col xs={24} sm={12}>
          <StatTile
            label={HOME_TEXT.payrolls}
            value={d.payrollsPending}
            tone={d.payrollsPending > 0 ? 'danger' : 'default'}
            to={PAYROLL_ROUTES.approval}
          />
        </Col>
      </Row>
      <SectionLabel>{HOME_TEXT.figures}</SectionLabel>
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        <Col xs={24} md={8}>
          <StatTile
            label={HOME_TEXT.ordersRunning}
            value={d.ordersRunning}
            tone="primary"
            note={<Typography.Text type="secondary">{HOME_TEXT.ordersRunningNote}</Typography.Text>}
            to={ROUTES.orders}
          />
        </Col>
        <Col xs={24} md={8}>
          <MoneyTile
            label={HOME_TEXT.revenue}
            money={d.revenue}
            upIsGood
            to={REVENUE_ROUTES.admin}
          />
        </Col>
        <Col xs={24} md={8}>
          <MoneyTile
            label={HOME_TEXT.cost}
            money={d.cost}
            upIsGood={false}
            to={COST_SUMMARY_ROUTES.admin}
          />
        </Col>
      </Row>
      <OrderProgressGrid title={HOME_TEXT.progress} orders={d.orders} />
    </>
  );
}
