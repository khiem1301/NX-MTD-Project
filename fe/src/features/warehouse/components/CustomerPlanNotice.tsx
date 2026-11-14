import { useState } from 'react';
import { Alert, Button } from 'antd';
import { formatDeliveryDates } from '@/utils/expectedDelivery';
import { fill } from '@/utils/format';
import { LABELS } from '../constants';
import { useCustomerDelivery } from '../hooks/useOrderMaterials';
import { CUSTOMER_DELIVERY_STATUS, customerDeliveryStatus } from '../utils/customerDeliveryStatus';
import { CustomerDeliveryPlanModal } from './CustomerDeliveryPlanModal';

/**
 * Nhắc ngay trong hộp thêm / sửa NVL (DEV-ALL-95): đơn đã nhận đủ số lần khách giao thì thêm / tăng NVL khách cấp sẽ bị chặn; lịch
 * đã quá ngày thì nên cập nhật ngày giao. Có nút mở Khai lịch giao để sửa luôn.
 */
export function CustomerPlanNotice({ orderId }: { orderId: number }) {
  const plan = useCustomerDelivery(orderId).data;
  const [editing, setEditing] = useState(false);
  const status = customerDeliveryStatus(plan);
  if (!plan || !status) return null;

  const full = status === CUSTOMER_DELIVERY_STATUS.FULL;
  const message = full
    ? fill(LABELS.customerDeliveryFullNotice, plan.deliveryCount ?? 1)
    : fill(
        LABELS.customerDeliveryLateNotice,
        formatDeliveryDates(plan.deliveryDates, plan.deliveryDate, plan.deliveryDateTo),
      );

  return (
    <>
      <Alert
        type={full ? 'error' : 'warning'}
        showIcon
        style={{ marginBottom: 12 }}
        message={message}
        action={
          <Button size="small" onClick={() => setEditing(true)}>
            {LABELS.editCustomerDelivery}
          </Button>
        }
      />
      <CustomerDeliveryPlanModal open={editing} plan={plan} onClose={() => setEditing(false)} />
    </>
  );
}
