import { Checkbox, Form } from 'antd';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.operations.form;

/** Ô tick công đoạn cuối: tick ở công đoạn khác thì cờ chuyển sang đó (mã hàng chỉ có một công đoạn cuối đang dùng). */
export function OperationFinalField({ hasOtherFinal }: { hasOtherFinal: boolean }) {
  return (
    <Form.Item
      name="isFinalStep"
      valuePropName="checked"
      style={{ marginBottom: 0 }}
      extra={hasOtherFinal ? text.isFinalMoves : undefined}
    >
      <Checkbox>{text.isFinal}</Checkbox>
    </Form.Item>
  );
}
