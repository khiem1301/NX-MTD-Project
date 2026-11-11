import { Col, Form, Row } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import { MATERIAL_QTY_DECIMALS, MAX_MATERIAL_QTY } from '@/constants/quantity';
import { LABELS } from '../constants';
import { qtyProblem } from '../utils/orderMaterialRules';

const required = [{ required: true, message: MESSAGES.common.required }];

/** Số cần, số khách cấp của một NVL trong đơn (lịch khách giao khai ở cả đơn — DEV-ALL-95). */
export function OrderMaterialQtyFields() {
  const form = Form.useFormInstance();

  return (
    <Row gutter={16}>
      <Col xs={24} sm={12}>
        <Form.Item
          name="requiredQty"
          label={LABELS.requiredQty}
          dependencies={['customerQty']}
          rules={[
            ...required,
            {
              validator: async (_rule, value?: number) => {
                const problem = qtyProblem(LABELS.requiredQtyName, value);
                if (problem) throw new Error(problem);
                const customer = form.getFieldValue('customerQty') as number | undefined;
                if ((value ?? 0) <= 0 && (customer ?? 0) <= 0)
                  throw new Error(LABELS.qtyRequiredOrCustomer);
              },
            },
          ]}
          initialValue={0}
        >
          <QtyInput
            decimals={MATERIAL_QTY_DECIMALS}
            max={MAX_MATERIAL_QTY}
            style={{ width: '100%' }}
          />
        </Form.Item>
      </Col>
      <Col xs={24} sm={12}>
        <Form.Item
          name="customerQty"
          label={LABELS.customerQty}
          rules={[
            ...required,
            {
              validator: async (_rule, value?: number) => {
                const problem = qtyProblem(LABELS.customerQtyName, value);
                if (problem) throw new Error(problem);
              },
            },
          ]}
          initialValue={0}
        >
          <QtyInput
            decimals={MATERIAL_QTY_DECIMALS}
            max={MAX_MATERIAL_QTY}
            style={{ width: '100%' }}
          />
        </Form.Item>
      </Col>
    </Row>
  );
}
