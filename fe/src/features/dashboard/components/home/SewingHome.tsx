import { useState } from 'react';
import { CloseOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Col, Progress, Row, Typography, theme } from 'antd';
import { useNavigate } from 'react-router-dom';
import { MEAL_ROUTES, PRODUCTION_ROUTES, ROUTES } from '@/constants/routes';
import { fill, formatQty } from '@/utils/format';
import { TeamKpiPanel } from '@/features/kpi/components/TeamKpiPanel';
import { HOME_ROLE_TEXT as T } from '../../constants';
import type { HomeItem, SewingHome as SewingHomeData } from '../../types';
import { HomeItemList } from './HomeItemList';
import { SectionLabel } from './SectionLabel';
import { StatTile } from './StatTile';

/** Phiếu bị chặn (đỏ) lên trước để tổ trưởng xử lý phân công trước khi nhận. */
const byBlockFirst = (items: HomeItem[]) =>
  [...items].sort((a, b) => Number(b.tagColor === 'red') - Number(a.tagColor === 'red'));

/** Trang chủ tổ trưởng / tổ phó tổ may: KPI hôm nay, việc cần xử lý, nhận BTP, KPI của tổ. */
export function SewingHome({ data: d }: { data: SewingHomeData }) {
  const navigate = useNavigate();
  const { token } = theme.useToken();
  const percent = d.kpiTarget > 0 ? Math.round((100 * d.kpiActual) / d.kpiTarget) : null;
  const meal = d.mealConfirmed ?? d.mealSuggested;
  const btpItems = byBlockFirst(d.btpIssues);
  const blocked = btpItems.filter((i) => i.tagColor === 'red').length;
  const waiting = btpItems.length - blocked;
  const hintKey = `${blocked}:${waiting}`;
  const [hiddenHintKey, setHiddenHintKey] = useState<string | null>(null);
  const showHint = btpItems.length > 0 && hiddenHintKey !== hintKey;

  return (
    <>
      <SectionLabel>{T.today}</SectionLabel>
      <Card size="small" title={T.kpiToday} style={{ marginBottom: 12 }}>
        <Row gutter={[16, 8]} align="middle">
          <Col xs={24} md={14}>
            <Progress
              percent={percent ?? 0}
              status={percent === null ? 'normal' : percent >= 100 ? 'success' : 'active'}
              format={() => (percent === null ? '—' : `${percent}%`)}
            />
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {fill(T.kpiTodayProgress, formatQty(d.kpiActual), formatQty(d.kpiTarget))}
            </Typography.Text>
          </Col>
          <Col xs={12} md={5}>
            <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
              {T.kpiTarget}
            </Typography.Text>
            <Typography.Text strong style={{ fontSize: 18 }}>
              {formatQty(d.kpiTarget)}
            </Typography.Text>
          </Col>
          <Col xs={12} md={5}>
            <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
              {T.kpiActual}
            </Typography.Text>
            <Typography.Text strong style={{ fontSize: 18, color: token.colorPrimary }}>
              {formatQty(d.kpiActual)}
            </Typography.Text>
          </Col>
        </Row>
      </Card>

      <SectionLabel>{T.needAction}</SectionLabel>
      <Row gutter={[12, 12]} style={{ marginBottom: 12 }}>
        <Col xs={12} sm={12} xl={6}>
          <StatTile
            label={T.tiles.btpPending}
            value={d.btpPending}
            tone={d.btpPending > 0 ? 'warn' : 'default'}
            to={ROUTES.materialIssues}
          />
        </Col>
        <Col xs={12} sm={12} xl={6}>
          <StatTile
            label={T.tiles.unassigned}
            value={d.unassignedOperations}
            tone={d.unassignedOperations > 0 ? 'danger' : 'default'}
            note={<Typography.Text type="secondary">{T.tiles.unassignedNote}</Typography.Text>}
            to={PRODUCTION_ROUTES.assignments}
          />
        </Col>
        <Col xs={12} sm={12} xl={6}>
          <StatTile
            label={T.tiles.allocations}
            value={d.allocationsPending}
            tone={d.allocationsPending > 0 ? 'primary' : 'default'}
            to={ROUTES.pieceMovements}
          />
        </Col>
        <Col xs={12} sm={12} xl={6}>
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

      <Row gutter={[12, 12]} align="stretch">
        <Col xs={24} xl={8}>
          <HomeItemList
            title={T.cards.btpIssues}
            items={btpItems}
            bodyMaxHeight={320}
            onOpen={() => navigate(ROUTES.materialIssues)}
            extra={
              showHint ? (
                <Alert
                  type={blocked > 0 ? 'error' : 'warning'}
                  showIcon
                  style={{ marginBottom: 8 }}
                  message={
                    blocked > 0
                      ? fill(T.btpBlockedHint, blocked)
                      : fill(T.btpWaitingHint, waiting)
                  }
                  action={
                    <Button
                      type="text"
                      size="small"
                      aria-label={T.hideHint}
                      icon={<CloseOutlined />}
                      onClick={(e) => {
                        e.stopPropagation();
                        setHiddenHintKey(hintKey);
                      }}
                    />
                  }
                />
              ) : undefined
            }
          />
        </Col>
        <Col xs={24} xl={16}>
          <TeamKpiPanel compact />
        </Col>
      </Row>
    </>
  );
}
