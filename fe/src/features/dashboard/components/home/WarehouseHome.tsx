import { Col, Row, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { ROUTES } from '@/constants/routes';
import { fill } from '@/utils/format';
import { HOME_ROLE_TEXT as T } from '../../constants';
import { useWarehouseHome } from '../../hooks/useHome';
import { HomeItemList } from './HomeItemList';
import { HomeState } from './HomeState';
import { SectionLabel } from './SectionLabel';
import { StatTile } from './StatTile';

/** Trang chủ Thủ kho: NVL chờ nhận, lệnh cắt chưa cấp vải, đơn xin chờ cấp, bàn giao chờ nhận. */
export function WarehouseHome() {
  const query = useWarehouseHome();
  const navigate = useNavigate();
  return (
    <HomeState query={query}>
      {(d) => {
        const handovers = d.cutHandovers + d.fgHandovers + d.returns;
        return (
          <>
            <SectionLabel>{T.waiting}</SectionLabel>
            <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
              <Col xs={24} sm={12} xl={6}>
                <StatTile
                  label={T.tiles.incoming}
                  value={d.incoming}
                  tone={d.incoming > 0 ? 'warn' : 'default'}
                  note={<Typography.Text type="secondary">{T.tiles.incomingNote}</Typography.Text>}
                  to={ROUTES.incomingMaterials}
                />
              </Col>
              <Col xs={24} sm={12} xl={6}>
                <StatTile
                  label={T.tiles.workOrdersNoIssue}
                  value={d.workOrdersNoIssue}
                  tone={d.workOrdersNoIssue > 0 ? 'primary' : 'default'}
                  to={ROUTES.materialIssues}
                />
              </Col>
              <Col xs={24} sm={12} xl={6}>
                <StatTile
                  label={T.tiles.requestsToIssue}
                  value={d.requestsToIssue}
                  tone={d.requestsToIssue > 0 ? 'primary' : 'default'}
                  to={ROUTES.requestInbox}
                />
              </Col>
              <Col xs={24} sm={12} xl={6}>
                <StatTile
                  label={T.tiles.handovers}
                  value={handovers}
                  tone={handovers > 0 ? 'warn' : 'default'}
                  note={
                    <Typography.Text type="secondary">
                      {fill(T.tiles.handoversNote, d.cutHandovers, d.fgHandovers, d.returns)}
                    </Typography.Text>
                  }
                  to={ROUTES.cutHandovers}
                />
              </Col>
            </Row>
            <Row gutter={[12, 12]}>
              <Col xs={24} lg={12}>
                <HomeItemList
                  title={T.cards.incomingItems}
                  items={d.incomingItems}
                  onOpen={() => navigate(ROUTES.incomingMaterials)}
                />
              </Col>
              <Col xs={24} lg={12}>
                <HomeItemList
                  title={T.cards.pendingItems}
                  items={d.pendingItems}
                  onOpen={() => navigate(ROUTES.fgHandovers)}
                />
              </Col>
            </Row>
          </>
        );
      }}
    </HomeState>
  );
}
