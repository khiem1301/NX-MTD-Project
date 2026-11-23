import { useState } from 'react';
import { Card, Col, Empty, Row } from 'antd';
import { HOME_TEXT } from '../../constants';
import type { OrderProgress } from '../../types';
import { OrderLinesModal } from '../OrderLinesModal';
import { OrderProgressPie } from './OrderProgressPie';

/** Lưới biểu đồ tròn tiến độ các đơn; bấm một đơn mở tiến độ từng dòng màu - size. */
export function OrderProgressGrid({ title, orders }: { title: string; orders: OrderProgress[] }) {
  const [viewing, setViewing] = useState<OrderProgress | null>(null);
  return (
    <Card title={title} size="small">
      {orders.length === 0 ? (
        <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={HOME_TEXT.noOrders} />
      ) : (
        <Row gutter={[12, 12]}>
          {orders.map((o) => (
            <Col key={o.orderId} xs={24} md={12}>
              <OrderProgressPie order={o} onOpen={setViewing} />
            </Col>
          ))}
        </Row>
      )}
      <OrderLinesModal order={viewing} onClose={() => setViewing(null)} />
    </Card>
  );
}
