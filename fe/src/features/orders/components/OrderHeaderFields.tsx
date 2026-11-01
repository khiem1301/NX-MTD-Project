import { PlusOutlined } from '@ant-design/icons';
import { Button, Col, Divider, Form, Input, Row, Select } from 'antd';
import type { Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MESSAGES } from '@/constants/messages';
import { zodRule } from '@/utils/zodRule';
import type { Customer } from '../types';
import { orderCodeSchema, orderNoteSchema } from '../utils/orderValidation';

type OrderHeaderFieldsProps = {
  customers: Customer[];
  /** Khách của đơn đang sửa: vẫn hiện dù đã ngừng hợp tác. */
  currentCustomerId?: number;
  /** Đơn không còn ở trạng thái Mới: không đổi mã đơn, khách hàng, ngày nhận. */
  locked: boolean;
  onAddCustomer: () => void;
};

const FIELD_STYLE = { marginBottom: 12 };
const FULL_WIDTH = { width: '100%' };

/** Thông tin chung của đơn: một hàng trên màn rộng, ghi chú ở hàng dưới. Dùng trong `Form` của OrderForm. */
export function OrderHeaderFields({
  customers,
  currentCustomerId,
  locked,
  onAddCustomer,
}: OrderHeaderFieldsProps) {
  return (
    <Row gutter={16}>
      <Col xs={24} sm={12} lg={5}>
        <Form.Item
          name="orderCode"
          label={MESSAGES.orders.orderCode}
          style={FIELD_STYLE}
          required
          rules={[zodRule(orderCodeSchema)]}
        >
          <Input disabled={locked} />
        </Form.Item>
      </Col>
      <Col xs={24} sm={12} lg={9}>
        <Form.Item
          name="customerId"
          label={MESSAGES.orders.customer}
          style={FIELD_STYLE}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <Select
            showSearch
            optionFilterProp="label"
            disabled={locked}
            options={customers
              .filter((c) => c.status === 'Active' || c.customerId === currentCustomerId)
              .map((c) => ({ value: c.customerId, label: c.customerName }))}
            dropdownRender={(menu) => (
              <>
                {menu}
                <Divider style={{ margin: '8px 0' }} />
                <Button type="link" icon={<PlusOutlined />} onClick={onAddCustomer}>
                  {MESSAGES.orders.addCustomer}
                </Button>
              </>
            )}
          />
        </Form.Item>
      </Col>
      <Col xs={12} lg={5}>
        <Form.Item
          name="receivedDate"
          label={MESSAGES.orders.receivedDate}
          style={FIELD_STYLE}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <AppDatePicker disabled={locked} style={FULL_WIDTH} />
        </Form.Item>
      </Col>
      <Col xs={12} lg={5}>
        <Form.Item
          name="dueDate"
          label={MESSAGES.orders.dueDate}
          style={FIELD_STYLE}
          dependencies={['receivedDate']}
          rules={[
            { required: true, message: MESSAGES.common.required },
            ({ getFieldValue }) => ({
              validator: (_, value: Dayjs | undefined) =>
                !value ||
                !getFieldValue('receivedDate') ||
                !value.isBefore(getFieldValue('receivedDate') as Dayjs, 'day')
                  ? Promise.resolve()
                  : Promise.reject(new Error(MESSAGES.orders.validation.dueBeforeReceived)),
            }),
          ]}
        >
          <AppDatePicker style={FULL_WIDTH} />
        </Form.Item>
      </Col>
      <Col span={24}>
        <Form.Item
          name="note"
          label={MESSAGES.orders.note}
          style={FIELD_STYLE}
          rules={[zodRule(orderNoteSchema)]}
        >
          <Input.TextArea autoSize={{ minRows: 1, maxRows: 3 }} />
        </Form.Item>
      </Col>
    </Row>
  );
}
