import { Form, Space } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { DeliveryDateFields } from '@/components/common/DeliveryDateFields';
import dayjs from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { LABELS } from '../constants';

/**
 * Ngày đặt hàng (không sau hôm nay), số lần nhà cung cấp dự kiến giao (1 đến 99) và ngày dự kiến giao
 * (không trước hôm nay / ngày đặt; giao từ 2 lần là khoảng ngày) của form tạo đơn mua.
 */
export function PurchaseOrderTimingFields() {
  const form = Form.useFormInstance();
  const orderDate = Form.useWatch('orderDate', form);
  const deliveryCount = Form.useWatch('expectedDeliveryCount', form);
  return (
    <Space wrap align="start">
      <Form.Item
        name="orderDate"
        label={LABELS.orderDate}
        rules={[{ required: true, message: LABELS.orderDateRequired }]}
      >
        <AppDatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
      </Form.Item>
      <Form.Item
        name="expectedDeliveryCount"
        label={LABELS.poExpectedDeliveryCount}
        rules={[
          { required: true, type: 'number', min: 1, message: LABELS.expectedDeliveryRequired },
        ]}
      >
        <QtyInput min={1} max={99} style={{ width: 170 }} />
      </Form.Item>
      <DeliveryDateFields
        count={deliveryCount}
        label={LABELS.expectedDeliveryDate}
        requiredMessage={LABELS.expectedDeliveryDateRequired}
        notBefore={orderDate}
      />
    </Space>
  );
}
