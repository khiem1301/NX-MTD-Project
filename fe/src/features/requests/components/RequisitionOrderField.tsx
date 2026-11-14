import { Form, Input, Modal, type FormInstance } from 'antd';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { EMPTY_LINE } from '../hooks/useRequestForm';

type RequisitionOrderFieldProps = {
  form: FormInstance;
  /** Sửa đơn: đơn hàng cố định (BE không đổi đơn hàng khi sửa), chỉ hiện mã đơn. */
  locked: boolean;
  orderCode: string | null;
};

type LineValue = { materialId?: number; orderLineId?: number };

/**
 * Đơn hàng của đơn Xin BTP / Xin NVL của tổ may (bắt buộc, DEV-ALL-110): danh sách chỉ có đơn được chia cho tổ (BE lọc).
 * Đổi đơn khi đã chọn hàng thì hỏi trước rồi xóa các dòng, vì NVL / mã hàng thuộc đơn cũ.
 */
export function RequisitionOrderField({ form, locked, orderCode }: RequisitionOrderFieldProps) {
  const orderId = Form.useWatch('orderId', form) as number | undefined;

  const handleChange = (next: number | undefined) => {
    const apply = () => form.setFieldsValue({ orderId: next, lines: [EMPTY_LINE] });
    const lines = (form.getFieldValue('lines') ?? []) as LineValue[];
    if (!lines.some((l) => l.materialId || l.orderLineId)) return apply();
    Modal.confirm({
      title: MESSAGES.requests.changeOrderTitle,
      content: MESSAGES.requests.changeTypeContent,
      okText: MESSAGES.common.confirm,
      cancelText: MESSAGES.common.cancel,
      onOk: apply,
    });
  };

  return (
    <Form.Item label={MESSAGES.requests.order} required>
      {locked ? (
        <Input value={orderCode ?? ''} disabled aria-label={MESSAGES.requests.order} />
      ) : (
        <OrderSelect
          width="100%"
          value={orderId}
          onChange={handleChange}
          placeholder={MESSAGES.requests.orderPlaceholder}
          notFoundContent={MESSAGES.requests.noOrders}
        />
      )}
      <Form.Item
        name="orderId"
        noStyle
        rules={[{ required: true, message: MESSAGES.common.required }]}
      >
        <Input type="hidden" />
      </Form.Item>
    </Form.Item>
  );
}
