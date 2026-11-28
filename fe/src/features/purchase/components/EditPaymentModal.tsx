import { confirmAmount } from '@/utils/confirmAmount';
import { useEffect } from 'react';
import { Form, Input, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { REASON_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { MoneyInput } from '@/components/common/MoneyInput';
import { formatCurrency, toApiDate } from '@/utils/format';
import { PAYMENT_METHOD_OPTIONS, PURCHASE_TEXT } from '../constants';
import { useUpdatePayment } from '../hooks/usePurchase';
import type { PaymentMethod, PurchaseOrderDetail, PurchasePayment } from '../types';
import { payableAmount } from '../utils/payable';

type Props = {
  order: PurchaseOrderDetail;
  /** Lần thanh toán đang sửa; null = đóng. */
  payment: PurchasePayment | null;
  onClose: () => void;
};

type FormValues = {
  paymentDate: Dayjs;
  amount: number | null;
  paymentMethod: PaymentMethod;
  paymentNote: string | null;
  reason: string;
};

const text = PURCHASE_TEXT.pay;

/** Sửa một lần thanh toán trong 24 giờ kể từ lúc nhập: bắt buộc lý do; tổng các lần không vượt giá trị thực tế. */
export function EditPaymentModal({ order, payment, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const update = useUpdatePayment(order.purchaseOrderId);
  // Số tối đa của lần này = giá trị thực tế trừ các lần khác.
  const maxAmount = payableAmount(order) - ((order.paidAmount ?? 0) - (payment?.amount ?? 0));

  useEffect(() => {
    if (!payment) return;
    form.setFieldsValue({
      paymentDate: dayjs(payment.paymentDate),
      amount: payment.amount,
      paymentMethod: payment.paymentMethod,
      paymentNote: payment.paymentNote,
      reason: '',
    });
  }, [payment, form]);

  const handleFinish = (values: FormValues) => {
    if (!payment) return;
    confirmAmount(text.amount, values.amount ?? 0, () => {
      update.mutate(
        {
          paymentId: payment.purchasePaymentId,
          body: {
            paymentDate: toApiDate(values.paymentDate),
            amount: values.amount ?? 0,
            paymentMethod: values.paymentMethod,
            paymentNote: values.paymentNote?.trim() || null,
            reason: values.reason.trim(),
            rowVer: order.rowVer,
          },
        },
        { onSuccess: onClose },
      );
    });
  };

  return (
    <Modal
      open={payment !== null}
      title={`${text.editTitle} · ${order.poCode}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={update.isPending}>
        <Form.Item
          name="paymentDate"
          label={text.date}
          rules={[{ required: true, message: text.dateRequired }]}
        >
          <AppDatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item
          name="amount"
          label={text.amount}
          rules={[
            { required: true, type: 'number', min: 0.01, message: text.amountRequired },
            {
              validator: async (_, value) => {
                if (typeof value !== 'number' || value <= maxAmount) return;
                throw new Error(text.amountExceedsTotal);
              },
            },
          ]}
          extra={`${text.maxForThis}: ${formatCurrency(maxAmount)}`}
        >
          <MoneyInput style={{ width: 240 }} />
        </Form.Item>
        <Form.Item
          name="paymentMethod"
          label={text.method}
          rules={[{ required: true, message: text.methodRequired }]}
        >
          <Select options={PAYMENT_METHOD_OPTIONS} style={{ width: 240 }} />
        </Form.Item>
        <Form.Item name="paymentNote" label={text.note}>
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
        <Form.Item
          name="reason"
          label={text.editReason}
          rules={[{ required: true, whitespace: true, message: text.editReasonRequired }]}
        >
          <Input.TextArea rows={2} maxLength={REASON_MAX_LENGTH} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
