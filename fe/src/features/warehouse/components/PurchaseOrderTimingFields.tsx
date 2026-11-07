import { Form, InputNumber, Space } from 'antd';
import dayjs from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { LABELS } from '../constants';
import { isExpectedDeliveryDateDisabled } from '@/utils/expectedDelivery';

/**
 * Ngày đặt hàng (không sau hôm nay), số lần nhà cung cấp dự kiến giao (1 đến 99) và ngày dự kiến giao
 * (không trước hôm nay / ngày đặt) của form tạo đơn mua.
 */
export function PurchaseOrderTimingFields() {
  const form = Form.useFormInstance();
  const orderDate = Form.useWatch('orderDate', form);
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
      <Form.Item
        name="expectedDeliveryDate"
        label={LABELS.expectedDeliveryDate}
        rules={[{ required: true, message: LABELS.expectedDeliveryDateRequired }]}
      >
        <AppDatePicker disabledDate={(d) => isExpectedDeliveryDateDisabled(d, orderDate)} />
      </Form.Item>
    </Space>
  );
}
