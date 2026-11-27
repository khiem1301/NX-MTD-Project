import { Card, Space, Typography } from 'antd';
import { useNavigate } from 'react-router-dom';
import { ROUTES } from '@/constants/routes';
import { fill } from '@/utils/format';
import { HOME_ROLE_TEXT as T } from '../../constants';
import type { HomeWorkOrder } from '../../types';
import { PIE_COLOR, pieGradient, sharePercents } from '../../utils/orderPie';

const PIE_SIZE = 88;

/** Biểu đồ tròn một lệnh cắt: đã cắt / còn lại theo kế hoạch. */
export function WorkOrderPie({ workOrder: w }: { workOrder: HomeWorkOrder }) {
  const navigate = useNavigate();
  const cut = Math.min(w.cutQty, w.plannedQty);
  const [percent = 0, restPercent = 0] = sharePercents([cut, w.plannedQty - cut], w.plannedQty);
  const background = pieGradient([
    { stage: 'cut', qty: cut, percent },
    { stage: 'rest', qty: Math.max(0, w.plannedQty - cut), percent: restPercent },
  ]);
  return (
    <Card
      size="small"
      hoverable
      onClick={() => navigate(ROUTES.workOrders)}
      style={{ height: '100%' }}
    >
      <Space align="start" size={14}>
        <div
          aria-label={w.code}
          style={{
            width: PIE_SIZE,
            height: PIE_SIZE,
            flex: 'none',
            borderRadius: '50%',
            background,
          }}
        />
        <div>
          <Typography.Text strong>
            {w.code} · {w.orderCode}
          </Typography.Text>
          <div>
            <Typography.Text type="secondary">
              {fill(T.workOrderPie, w.cutQty, w.plannedQty)}
            </Typography.Text>
          </div>
          <span style={{ fontSize: 11 }}>
            <i
              style={{
                display: 'inline-block',
                width: 8,
                height: 8,
                borderRadius: 2,
                marginRight: 4,
                background: PIE_COLOR.cut,
              }}
            />
            {percent}%
          </span>
        </div>
      </Space>
    </Card>
  );
}
