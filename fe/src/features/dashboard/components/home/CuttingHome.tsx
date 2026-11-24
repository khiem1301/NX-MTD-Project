import { Card, Col, Empty, Row, Typography } from 'antd';
import { ROUTES } from '@/constants/routes';
import { HOME_ROLE_TEXT as T } from '../../constants';
import type { CuttingHome as CuttingHomeData } from '../../types';
import { SectionLabel } from './SectionLabel';
import { StatTile } from './StatTile';
import { WorkOrderPie } from './WorkOrderPie';

/** Trang chủ tổ trưởng / tổ phó tổ cắt: phiếu cấp vải, BTP chưa bàn giao, vải thừa chưa trả, lệnh cắt đang mở. */
export function CuttingHome({ data: d }: { data: CuttingHomeData }) {
  return (
    <>
      <SectionLabel>{T.team}</SectionLabel>
      <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
        <Col xs={24} md={8}>
          <StatTile
            label={T.tiles.cutIssues}
            value={d.issuesPending}
            tone={d.issuesPending > 0 ? 'warn' : 'default'}
            to={ROUTES.materialIssues}
          />
        </Col>
        <Col xs={24} md={8}>
          <StatTile
            label={T.tiles.cutNotHanded}
            value={d.cutNotHanded}
            note={<Typography.Text type="secondary">{T.tiles.cutNotHandedNote}</Typography.Text>}
            to={ROUTES.cutHandovers}
          />
        </Col>
        <Col xs={24} md={8}>
          <StatTile
            label={T.tiles.leftover}
            value={d.leftoverNotReturned}
            tone={d.leftoverNotReturned > 0 ? 'danger' : 'default'}
            to={ROUTES.materialReturns}
          />
        </Col>
      </Row>
      <Card title={T.cards.workOrders} size="small">
        {d.workOrders.length === 0 ? (
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={T.empty} />
        ) : (
          <Row gutter={[12, 12]}>
            {d.workOrders.map((w) => (
              <Col key={w.workOrderId} xs={24} md={12}>
                <WorkOrderPie workOrder={w} />
              </Col>
            ))}
          </Row>
        )}
      </Card>
    </>
  );
}
