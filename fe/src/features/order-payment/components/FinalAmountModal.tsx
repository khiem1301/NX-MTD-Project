import { useEffect } from 'react';
import { Form, Input, InputNumber, Modal } from 'antd';
import { REASON_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency } from '@/utils/format';
import { ORDER_PAYMENT_TEXT } from '../constants';
import { useSetFinalAmount } from '../hooks/useOrderPayment';
import type { OrderPaymentDetail } from '../types';
import { HintTip } from '@/components/common/HintTip';

type Props = { order: OrderPaymentDetail; open: boolean; onClose: () => void };
type FormValues = { amount: number | null; reason: string };

const text = ORDER_PAYMENT_TEXT.final;

/** Chốt hoặc đổi giá trị cả đơn hàng (cho phép âm khi mình phải đền hợp đồng), bắt buộc lý do. */
export function FinalAmountModal({ order, open, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const save = useSetFinalAmount(order.orderId);
  const paid = order.paidAmount;

  useEffect(() => {
    if (!open) return;
    form.setFieldsValue({ amount: order.finalAmount, reason: '' });
  }, [open, order, form]);

  return (
    <Modal
      open={open}
      title={`${text.title} · ${order.orderCode}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <HintTip text={text.amountHint} />
      <Form
        form={form}
        layout="vertical"
        disabled={save.isPending}
        onFinish={(v) =>
          save.mutate(
            { amount: v.amount ?? 0, reason: v.reason.trim(), rowVer: order.rowVer },
            { onSuccess: onClose },
          )
        }
      >
        <Form.Item
          name="amount"
          label={text.amount}
          validateTrigger="onSubmit"
          rules={[
            { required: true, type: 'number', message: text.amountRequired },
            {
              validator: async (_, value) => {
                if (typeof value !== 'number') return;
                const inRange = paid >= Math.min(0, value) && paid <= Math.max(0, value);
                if (!inRange) throw new Error(ORDER_PAYMENT_TEXT.pay.outOfRange);
              },
            },
          ]}
          extra={`${ORDER_PAYMENT_TEXT.detail.paid}: ${formatCurrency(paid)}`}
        >
          <InputNumber precision={0} addonAfter="₫" style={{ width: 260 }} />
        </Form.Item>
        <Form.Item
          name="reason"
          label={text.reason}
          rules={[{ required: true, whitespace: true, message: text.reasonRequired }]}
        >
          <Input.TextArea rows={2} maxLength={REASON_MAX_LENGTH} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
