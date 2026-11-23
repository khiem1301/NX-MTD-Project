import { Card, Col, Row, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { MEAL_ROUTES, PRODUCTION_ROUTES, ROUTES } from '@/constants/routes';
import { formatQty } from '@/utils/format';
import { HOME_ROLE_TEXT as T } from '../../constants';
import type { SewingHome as SewingHomeData } from '../../types';
import { HomeItemList } from './HomeItemList';
import { SectionLabel } from './SectionLabel';
import { StatTile } from './StatTile';

/** Trang chủ tổ trưởng / tổ phó tổ may: KPI hôm nay, phiếu BTP chờ nhận (có cảnh báo chặn), giao việc, suất ăn. */
export function SewingHome({ data: d }: { data: SewingHomeData }) {
  const navigate = useNavigate();
  const percent = d.kpiTarget > 0 ? Math.round((100 * d.kpiActual) / d.kpiTarget) : null;
  const meal = d.mealConfirmed ?? d.mealSuggested;
  return (
    <>
      <SectionLabel>{T.team}</SectionLabel>
      <Card title={T.kpiToday} size="small" style={{ marginBottom: 12 }}>
        <Row gutter={[12, 12]}>
          <Col xs={24} sm={8}>
            <StatTile
              label={T.kpiAchieved}
              value={percent === null ? '—' : `${percent}%`}
              tone={percent !== null && percent >= 100 ? 'ok' : 'default'}
            />
          </Col>
          <Col xs={12} sm={8}>
            <StatTile label={T.kpiTarget} value={formatQty(d.kpiTarget)} />
          </Col>
          <Col xs={12} sm={8}>
            <StatTile label={T.kpiActual} value={formatQty(d.kpiActual)} tone="primary" />
          </Col>
        </Row>
      </Card>
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        <Col xs={24} sm={12} xl={6}>
          <StatTile
            label={T.tiles.btpPending}
            value={d.btpPending}
            tone={d.btpPending > 0 ? 'warn' : 'default'}
            to={ROUTES.materialIssues}
          />
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <StatTile
            label={T.tiles.unassigned}
            value={d.unassignedOperations}
            tone={d.unassignedOperations > 0 ? 'danger' : 'default'}
            note={<Typography.Text type="secondary">{T.tiles.unassignedNote}</Typography.Text>}
            to={PRODUCTION_ROUTES.assignments}
          />
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <StatTile
            label={T.tiles.allocations}
            value={d.allocationsPending}
            tone={d.allocationsPending > 0 ? 'primary' : 'default'}
            to={ROUTES.pieceMovements}
          />
        </Col>
        <Col xs={24} sm={12} xl={6}>
          <StatTile
            label={T.tiles.meal}
            value={meal}
            tone="ok"
            note={
              <Typography.Text type="secondary">
                {d.mealLocked ? T.tiles.mealLocked : T.tiles.mealSuggested}
              </Typography.Text>
            }
            to={MEAL_ROUTES.teamMeal}
          />
        </Col>
      </Row>
      <HomeItemList
        title={T.cards.btpIssues}
        items={d.btpIssues}
        onOpen={() => navigate(ROUTES.materialIssues)}
      />
    </>
  );
}
