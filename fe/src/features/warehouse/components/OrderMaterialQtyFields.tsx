import { Col, Form, InputNumber, Row } from 'antd';
import { AppDatePicker } from '@/components/common/DateInputs';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import { MATERIAL_QTY_DECIMALS, MAX_MATERIAL_QTY } from '@/constants/quantity';
import { isExpectedDeliveryDateDisabled } from '@/utils/expectedDelivery';
import { LABELS } from '../constants';
import { deliveryCountProblem, qtyProblem } from '../utils/orderMaterialRules';

type OrderMaterialQtyFieldsProps = {
  /** Số khách cấp đang nhập: > 0 thì bắt buộc số lần giao và ngày giao dự kiến. */
  customerQty: number | undefined;
};

const required = [{ required: true, message: MESSAGES.common.required }];

/** Số cần, số khách cấp, số lần giao, ngày giao dự kiến của một NVL trong đơn. */
export function OrderMaterialQtyFields({ customerQty }: OrderMaterialQtyFieldsProps) {
  const form = Form.useFormInstance();
  const hasCustomer = (customerQty ?? 0) > 0;

  return (
    <>
      <Row gutter={16}>
        <Col xs={24} sm={8}>
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
        <Col xs={24} sm={8}>
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
        <Col xs={24} sm={8}>
          <Form.Item
            name="expectedDeliveryCount"
            label={LABELS.expectedDeliveryCount}
            rules={[
              { required: hasCustomer, message: MESSAGES.common.required },
              {
                validator: async (_rule, value?: number) => {
                  const problem = deliveryCountProblem(value);
                  if (problem) throw new Error(problem);
                },
              },
            ]}
          >
            <InputNumber disabled={!hasCustomer} style={{ width: '100%' }} />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={12}>
        <Col xs={24} sm={8}>
          <Form.Item
            name="expectedDeliveryDate"
            label={LABELS.expectedDeliveryDate}
            rules={[{ required: hasCustomer, message: LABELS.expectedDeliveryDateRequired }]}
          >
            <AppDatePicker
              disabled={!hasCustomer}
              style={{ width: '100%' }}
              disabledDate={(d) => isExpectedDeliveryDateDisabled(d)}
            />
          </Form.Item>
        </Col>
      </Row>
    </>
  );
}
