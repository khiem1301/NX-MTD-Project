import { useState } from 'react';
import { CalendarOutlined } from '@ant-design/icons';
import { Alert, Button, Space, Typography } from 'antd';
import { formatDeliveryDates } from '@/utils/expectedDelivery';
import { fill } from '@/utils/format';
import { LABELS } from '../constants';
import { useCustomerDelivery } from '../hooks/useOrderMaterials';
import { CUSTOMER_DELIVERY_STATUS, customerDeliveryStatus } from '../utils/customerDeliveryStatus';
import { CustomerDeliveryPlanModal } from './CustomerDeliveryPlanModal';

type CustomerDeliveryPlanProps = {
  orderId: number;
  /** Đơn đã đóng: chỉ xem. */
  isClosed: boolean;
};

/**
 * Lịch khách giao NVL của cả đơn (DEV-ALL-95) ở đầu tab NVL của đơn: đã giao mấy / mấy lần, ngày dự kiến. Chỉ hiện khi đơn có NVL
 * khách cấp (khách không cấp gì thì không phải khai); chưa khai thì nhắc (kho coi như giao 1 lần).
 */
export function CustomerDeliveryPlan({ orderId, isClosed }: CustomerDeliveryPlanProps) {
  const plan = useCustomerDelivery(orderId);
  const [editing, setEditing] = useState(false);
  const data = plan.data;
  if (!data?.hasCustomerMaterials) return null; // khách không cấp NVL nào thì không cần lịch giao

  const declared = data.deliveryCount !== null;
  const late = customerDeliveryStatus(data) === CUSTOMER_DELIVERY_STATUS.LATE;
  const summary = declared
    ? fill(
        LABELS.customerDeliveryValue,
        data.receivedCount,
        data.deliveryCount ?? 1,
        formatDeliveryDates(data.deliveryDates, data.deliveryDate, data.deliveryDateTo),
      )
    : LABELS.customerDeliveryMissing;

  return (
    <>
      <Alert
        type={declared && !late ? 'info' : 'warning'}
        showIcon
        icon={<CalendarOutlined />}
        style={{ marginBottom: 12 }}
        message={
          <Space wrap>
            <Typography.Text strong>{LABELS.customerDeliveryPlan}:</Typography.Text>
            <Typography.Text>{summary}</Typography.Text>
            {late && (
              <Typography.Text type="warning">{LABELS.customerDeliveryLateTag}</Typography.Text>
            )}
          </Space>
        }
        action={
          !isClosed && (
            <Button size="small" onClick={() => setEditing(true)}>
              {LABELS.editCustomerDelivery}
            </Button>
          )
        }
      />
      <CustomerDeliveryPlanModal open={editing} plan={data} onClose={() => setEditing(false)} />
    </>
  );
}
