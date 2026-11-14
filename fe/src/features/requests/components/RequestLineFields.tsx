import { DeleteOutlined, PlusOutlined } from '@ant-design/icons';
import { Button, Col, Form, Input, Row, Typography } from 'antd';
import { QtyInput } from '@/components/common/QtyInput';
import { MESSAGES } from '@/constants/messages';
import type { RequestType } from '@/constants/requestTypes';
import { REQUEST_LINE_LIMITS } from '../constants';
import { EMPTY_LINE } from '../hooks/useRequestForm';
import { lineQuantityRule } from '../utils/lineQuantity';

type RequestLineFieldsProps = {
  requestType: RequestType | undefined;
};

const ITEM_SPAN = { xs: 24, sm: 17 };
const QTY_SPAN = { xs: 21, sm: 6 };
const DELETE_SPAN = { xs: 3, sm: 1 };

/**
 * Dòng của đơn gõ tên hàng (Cơ điện xin linh kiện, vật tư, máy…), dạng bảng có tiêu đề cột. Đơn Xin NVL / Xin BTP dùng
 * RequisitionLineFields (chọn hàng, có cột khả dụng trong kho).
 */
export function RequestLineFields({ requestType }: RequestLineFieldsProps) {
  const itemLabel = MESSAGES.requests.itemDescription;

  return (
    <Form.List name="lines">
      {(fields, { add, remove }) => (
        <>
          <Row gutter={8} style={{ marginBottom: 4 }}>
            <Col {...ITEM_SPAN}>
              <Typography.Text strong>{itemLabel}</Typography.Text>
            </Col>
            <Col {...QTY_SPAN}>
              <Typography.Text strong>{MESSAGES.requests.quantity}</Typography.Text>
            </Col>
          </Row>
          {fields.map((f) => (
            <Row key={f.key} gutter={8} align="top">
              <Col {...ITEM_SPAN}>
                <Form.Item
                  name={[f.name, 'itemDescription']}
                  rules={[
                    { required: true, whitespace: true, message: MESSAGES.common.required },
                    { max: 300 },
                  ]}
                >
                  <Input placeholder={itemLabel} aria-label={itemLabel} />
                </Form.Item>
              </Col>
              <Col {...QTY_SPAN}>
                <Form.Item
                  name={[f.name, 'quantity']}
                  rules={[{ required: true, message: MESSAGES.common.required }]}
                >
                  <QtyInput
                    {...lineQuantityRule(requestType, undefined)}
                    style={{ width: '100%' }}
                    aria-label={MESSAGES.requests.quantity}
                  />
                </Form.Item>
              </Col>
              <Col {...DELETE_SPAN}>
                {fields.length > 1 && (
                  <Button
                    type="text"
                    danger
                    icon={<DeleteOutlined />}
                    aria-label={MESSAGES.common.delete}
                    onClick={() => remove(f.name)}
                  />
                )}
              </Col>
            </Row>
          ))}
          <Button
            type="dashed"
            block
            icon={<PlusOutlined />}
            disabled={fields.length >= REQUEST_LINE_LIMITS.maxLines}
            onClick={() => add(EMPTY_LINE)}
          >
            {MESSAGES.requests.addLine}
          </Button>
        </>
      )}
    </Form.List>
  );
}
