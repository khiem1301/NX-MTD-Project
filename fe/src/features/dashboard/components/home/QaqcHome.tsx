import { Col, Row } from 'antd';
import { useNavigate } from 'react-router-dom';
import { PRODUCTION_ROUTES, ROUTES } from '@/constants/routes';
import { HOME_ROLE_TEXT as T } from '../../constants';
import { useQaqcHome } from '../../hooks/useHome';
import { HomeItemList } from './HomeItemList';
import { HomeState } from './HomeState';
import { SectionLabel } from './SectionLabel';
import { StatTile } from './StatTile';

/** Trang chủ QA/QC: hàng trên chuyền chưa kiểm, hoàn thiện chưa nhập kho, bàn giao thành phẩm chờ kho. */
export function QaqcHome() {
  const query = useQaqcHome();
  const navigate = useNavigate();
  return (
    <HomeState query={query}>
      {(d) => (
        <>
          <SectionLabel>QA / QC</SectionLabel>
          <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
            <Col xs={24} md={8}>
              <StatTile
                label={T.tiles.onLine}
                value={d.onLineQty}
                tone={d.onLineQty > 0 ? 'warn' : 'default'}
                to={PRODUCTION_ROUTES.qcBatches}
              />
            </Col>
            <Col xs={24} md={8}>
              <StatTile
                label={T.tiles.notReceived}
                value={d.notReceivedQty}
                tone={d.notReceivedQty > 0 ? 'primary' : 'default'}
                to={ROUTES.fgHandovers}
              />
            </Col>
            <Col xs={24} md={8}>
              <StatTile
                label={T.tiles.handoversQa}
                value={d.handoversPending}
                tone={d.handoversPending > 0 ? 'warn' : 'default'}
                to={ROUTES.fgHandovers}
              />
            </Col>
          </Row>
          <Row gutter={[12, 12]}>
            <Col xs={24} lg={12}>
              <HomeItemList
                title={T.cards.qaLines}
                items={d.lines}
                onOpen={() => navigate(PRODUCTION_ROUTES.qcBatches)}
              />
            </Col>
            <Col xs={24} lg={12}>
              <HomeItemList
                title={T.cards.qaHandovers}
                items={d.handovers}
                onOpen={() => navigate(ROUTES.fgHandovers)}
              />
            </Col>
          </Row>
        </>
      )}
    </HomeState>
  );
}
