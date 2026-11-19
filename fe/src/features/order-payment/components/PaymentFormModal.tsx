import { useEffect } from 'react';
import { Form, Input, InputNumber, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { REASON_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, toApiDate } from '@/utils/format';
import { ORDER_PAYMENT_METHOD_OPTIONS, ORDER_PAYMENT_TEXT } from '../constants';
import { useAddOrderPayment, useUpdateOrderPayment } from '../hooks/useOrderPayment';
import type { OrderPaymentDetail, OrderPaymentLine, OrderPaymentMethod } from '../types';
import { allowedRange } from '../utils/range';

type Props = {
  order: OrderPaymentDetail;
  open: boolean;
  /** Có = sửa lần thanh toán này (bắt buộc lý do); null = thêm mới. */
  editing: OrderPaymentLine | null;
  onClose: () => void;
};

type FormValues = {
  paymentDate: Dayjs;
  amount: number | null;
  paymentMethod: OrderPaymentMethod;
  paymentNote: string | null;
  reason: string;
};

const text = ORDER_PAYMENT_TEXT.pay;

/** Thêm hoặc sửa một lần thanh toán đơn hàng: dương = khách trả, âm = mình trả; tổng luôn nằm giữa 0 và giá trị chốt. */
export function PaymentFormModal({ order, open, editing, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const add = useAddOrderPayment(order.orderId);
  const update = useUpdateOrderPayment(order.orderId);
  const pending = add.isPending || update.isPending;
  const range = allowedRange(order.finalAmount ?? 0, order.paidAmount - (editing?.amount ?? 0));

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue(
      editing
        ? {
            paymentDate: dayjs(editing.paymentDate),
            amount: editing.amount,
            paymentMethod: editing.paymentMethod,
            paymentNote: editing.paymentNote,
          }
        : { paymentDate: dayjs(), paymentMethod: 'BankTransfer' },
    );
  }, [open, editing, form]);

  const handleFinish = (values: FormValues) => {
    const body = {
      paymentDate: toApiDate(values.paymentDate),
      amount: values.amount ?? 0,
      paymentMethod: values.paymentMethod,
      paymentNote: values.paymentNote?.trim() || null,
      rowVer: order.rowVer,
    };
    if (editing) {
      update.mutate(
        { paymentId: editing.orderPaymentId, body: { ...body, reason: values.reason.trim() } },
        { onSuccess: onClose },
      );
    } else {
      add.mutate(body, { onSuccess: onClose });
    }
  };

  return (
    <Modal
      open={open}
      title={`${editing ? text.editTitle : text.addTitle} · ${order.orderCode}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={pending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={pending}>
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
          validateTrigger="onSubmit"
          rules={[
            { required: true, type: 'number', message: text.amountRequired },
            {
              validator: async (_, value) => {
                if (typeof value !== 'number') return;
                if (value === 0) throw new Error(text.amountRequired);
                if (value < range.min || value > range.max) throw new Error(text.outOfRange);
              },
            },
          ]}
          extra={`${text.amountHint} ${text.allowed}: ${formatCurrency(range.min)} → ${formatCurrency(range.max)}`}
        >
          <InputNumber precision={0} addonAfter="₫" style={{ width: 260 }} />
        </Form.Item>
        <Form.Item
          name="paymentMethod"
          label={text.method}
          rules={[{ required: true, message: text.methodRequired }]}
        >
          <Select options={ORDER_PAYMENT_METHOD_OPTIONS} style={{ width: 240 }} />
        </Form.Item>
        <Form.Item name="paymentNote" label={text.note}>
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
        {editing && (
          <Form.Item
            name="reason"
            label={text.editReason}
            rules={[{ required: true, whitespace: true, message: text.editReasonRequired }]}
          >
            <Input.TextArea rows={2} maxLength={REASON_MAX_LENGTH} showCount />
          </Form.Item>
        )}
      </Form>
    </Modal>
  );
}
