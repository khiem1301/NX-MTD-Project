import { Card, Col, Row, Tag, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { MEAL_ROUTES, PAYROLL_ROUTES, PRODUCTION_ROUTES, ROUTES } from '@/constants/routes';
import { formatCurrency } from '@/utils/format';
import { HOME_ROLE_TEXT as T } from '../../constants';
import { useWorkerHome } from '../../hooks/useHome';
import type { WorkerHome as WorkerHomeData } from '../../types';
import { HomeItemList } from './HomeItemList';
import { HomeState } from './HomeState';
import { StatTile } from './StatTile';

const mealTag = (d: WorkerHomeData) => {
  if (!d.mealHasAttendance) return { text: T.meal.noAttendance, color: 'default' };
  if (d.mealLocked) return { text: T.meal.locked, color: 'default' };
  return d.mealWillEat
    ? { text: T.meal.willEat, color: 'green' }
    : { text: T.meal.wontEat, color: 'orange' };
};

/** Trang chủ công nhân (dùng được trên điện thoại): lương tạm tính, phiếu lương, việc chờ xác nhận, phiếu lỗi, suất ăn. */
export function WorkerHome() {
  const query = useWorkerHome();
  const navigate = useNavigate();
  return (
    <HomeState query={query}>
      {(d) => {
        const meal = mealTag(d);
        return (
          <>
            <Row gutter={[12, 12]} style={{ marginBottom: 12 }}>
              {d.estimatedMonth !== null && (
                <Col xs={24} md={12}>
                  <StatTile
                    label={T.pay.today}
                    value={formatCurrency(d.estimatedToday ?? 0)}
                    tone="primary"
                    note={
                      <Typography.Text type="secondary">
                        {T.pay.month}: <strong>{formatCurrency(d.estimatedMonth)}</strong> ·{' '}
                        {T.pay.estimate}
                      </Typography.Text>
                    }
                    to={PAYROLL_ROUTES.myEstimatedPay}
                  />
                </Col>
              )}
              <Col xs={24} md={12}>
                <StatTile
                  label={T.cards.payslip}
                  value={d.latestPayslip ? formatCurrency(d.latestPayslip.netPay) : '—'}
                  note={
                    <Typography.Text type="secondary">
                      {d.latestPayslip ? d.latestPayslip.periodMonth : T.noPayslip}
                    </Typography.Text>
                  }
                  to={PAYROLL_ROUTES.myPayslip}
                />
              </Col>
            </Row>
            <Row gutter={[12, 12]}>
              <Col xs={24} md={12}>
                <HomeItemList
                  title={T.cards.allocations}
                  items={d.allocations}
                  onOpen={() => navigate(ROUTES.myPieceMovements)}
                />
              </Col>
              <Col xs={24} md={12}>
                <HomeItemList
                  title={T.cards.defects}
                  items={d.defects}
                  onOpen={() => navigate(PRODUCTION_ROUTES.myDefects)}
                />
              </Col>
              <Col xs={24}>
                <Card
                  size="small"
                  title={T.cards.mealToday}
                  hoverable
                  onClick={() => navigate(MEAL_ROUTES.myMeal)}
                  extra={<Tag color={meal.color}>{meal.text}</Tag>}
                />
              </Col>
            </Row>
          </>
        );
      }}
    </HomeState>
  );
}
