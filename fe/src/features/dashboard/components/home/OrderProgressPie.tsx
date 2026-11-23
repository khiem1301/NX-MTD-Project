import { Card, Space, Typography, theme } from 'antd';
import { fill, formatDate } from '@/utils/format';
import { HOME_TEXT, ORDER_DELAY, ORDER_DELAY_TEXT } from '../../constants';
import type { OrderProgress } from '../../types';
import { orderDelayOf } from '../../utils/orderDelay';
import { PIE_COLOR, orderPieSlices, pieGradient, type PieStage } from '../../utils/orderPie';

const PIE_SIZE = 88;
const LEGEND_STAGES: Exclude<PieStage, 'rest'>[] = ['cut', 'sew', 'qc', 'ship'];

/** Biểu đồ tròn tiến độ một đơn: vị trí hiện tại của hàng theo từng chặng, kèm cảnh báo trễ / nguy cơ trễ. */
export function OrderProgressPie({
  order,
  onOpen,
}: {
  order: OrderProgress;
  onOpen: (order: OrderProgress) => void;
}) {
  const { token } = theme.useToken();
  const slices = orderPieSlices(order);
  const delay = orderDelayOf(order);
  const late = delay === ORDER_DELAY.LATE;
  const warnColor = late ? token.colorError : token.colorWarning;
  return (
    <Card size="small" hoverable onClick={() => onOpen(order)} style={{ height: '100%' }}>
      <Space align="start" size={14}>
        <div
          aria-label={order.orderCode}
          style={{
            width: PIE_SIZE,
            height: PIE_SIZE,
            flex: 'none',
            borderRadius: '50%',
            background: pieGradient(slices),
            boxShadow: 'inset 0 0 0 1px rgba(0,0,0,.04)',
          }}
        />
        <div style={{ minWidth: 0 }}>
          <Typography.Text strong>{order.orderCode}</Typography.Text>
          <div>
            <Typography.Text type="secondary">
              {order.customerName} · {formatDate(order.dueDate)}
            </Typography.Text>
          </div>
          <Space size={[10, 2]} wrap style={{ marginTop: 6, fontSize: 11 }}>
            {LEGEND_STAGES.map((stage) => (
              <span key={stage}>
                <i
                  style={{
                    display: 'inline-block',
                    width: 8,
                    height: 8,
                    borderRadius: 2,
                    marginRight: 4,
                    background: PIE_COLOR[stage],
                  }}
                />
                {HOME_TEXT.stage[stage]} {slices.find((s) => s.stage === stage)?.percent ?? 0}%
              </span>
            ))}
          </Space>
          {delay && (
            <div style={{ marginTop: 8, color: warnColor, fontSize: 12 }}>
              {late
                ? fill(ORDER_DELAY_TEXT.lateDays, -order.daysLeft)
                : `${ORDER_DELAY_TEXT.risk} · ${fill(ORDER_DELAY_TEXT.daysLeft, order.daysLeft)}`}
            </div>
          )}
          {order.materialStatus === 'Awaiting' && (
            <div style={{ marginTop: 8, color: token.colorWarning, fontSize: 12 }}>
              {HOME_TEXT.waitMaterial}
            </div>
          )}
        </div>
      </Space>
    </Card>
  );
}
