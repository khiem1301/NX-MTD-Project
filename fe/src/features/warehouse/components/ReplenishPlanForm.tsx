import { Form, InputNumber, Space, type FormInstance } from 'antd';
import { DeliveryDateFields } from '@/components/common/DeliveryDateFields';
import { LABELS } from '../constants';
import { MAX_DELIVERY_COUNT } from '../utils/orderMaterialRules';
import type { ReplenishPlanValues } from '../utils/replenishPlan';

/** Quản đốc chọn giao bù: bên giao dự kiến giao bù mấy lần và ngày nào (giao từ 2 lần: khoảng ngày). */
export function ReplenishPlanForm({ form }: { form: FormInstance<ReplenishPlanValues> }) {
  const count = Form.useWatch('expectedDeliveryCount', form);
  return (
    <Form
      form={form}
      layout="vertical"
      initialValues={{ expectedDeliveryCount: 1 }}
      style={{ marginTop: 16 }}
    >
      <Space wrap align="start">
        <Form.Item
          name="expectedDeliveryCount"
          label={LABELS.replenishDeliveryCount}
          rules={[
            { required: true, type: 'number', min: 1, message: LABELS.expectedDeliveryRequired },
          ]}
        >
          <InputNumber min={1} max={MAX_DELIVERY_COUNT} precision={0} style={{ width: 170 }} />
        </Form.Item>
        <DeliveryDateFields
          count={count}
          label={LABELS.replenishDate}
          requiredMessage={LABELS.replenishDateRequired}
        />
      </Space>
    </Form>
  );
}
