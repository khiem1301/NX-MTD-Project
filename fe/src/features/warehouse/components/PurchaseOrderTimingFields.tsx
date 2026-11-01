import { Form, InputNumber, Space } from 'antd';
import dayjs from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { LABELS } from '../constants';

/** Ngày đặt hàng (không sau hôm nay) và số lần nhà cung cấp dự kiến giao (1 đến 99) của form tạo đơn mua. */
export function PurchaseOrderTimingFields() {
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
        <InputNumber min={1} max={99} precision={0} style={{ width: 170 }} />
      </Form.Item>
    </Space>
  );
}
