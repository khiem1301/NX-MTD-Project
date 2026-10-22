import { useEffect } from 'react';
import { Alert, DatePicker, Form, Input, InputNumber, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, toApiDate } from '@/utils/format';
import { PAYMENT_METHOD_OPTIONS, PURCHASE_TEXT } from '../constants';
import { useConfirmPayment } from '../hooks/usePurchase';
import type { PaymentMethod, PurchaseOrderDetail } from '../types';

type Props = { open: boolean; order: PurchaseOrderDetail; onClose: () => void };

type FormValues = {
  paymentDate: Dayjs;
  paidAmount: number | null;
  paymentMethod: PaymentMethod;
  paymentNote: string | null;
};

const DATE_FORMAT = 'DD/MM/YYYY';

/** UC34: Kế toán xác nhận thanh toán đơn mua. Ảnh chứng từ không bắt buộc, đính ở khung ảnh bên ngoài. */
export function ConfirmPaymentModal({ open, order, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const confirm = useConfirmPayment(order.purchaseOrderId);
  const text = PURCHASE_TEXT.pay;
  const amount = Form.useWatch('paidAmount', form);
  const differs = typeof amount === 'number' && amount > 0 && Math.round(amount) !== Math.round(order.totalAmount);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({
      paymentDate: dayjs(),
      paidAmount: order.totalAmount > 0 ? order.totalAmount : null,
      paymentMethod: 'BankTransfer',
    });
  }, [open, order, form]);

  const handleFinish = (values: FormValues) => {
    confirm.mutate(
      {
        paymentDate: toApiDate(values.paymentDate),
        paidAmount: values.paidAmount ?? 0,
        paymentMethod: values.paymentMethod,
        paymentNote: values.paymentNote?.trim() || null,
        rowVer: order.rowVer,
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      title={`${text.title} · ${order.poCode}`}
      okText={PURCHASE_TEXT.actions.confirmPayment}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={confirm.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={confirm.isPending}>
        <Form.Item name="paymentDate" label={text.date} rules={[{ required: true, message: text.dateRequired }]}>
          <DatePicker format={DATE_FORMAT} disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item
          name="paidAmount"
          label={text.amount}
          rules={[{ required: true, type: 'number', min: 0.01, message: text.amountRequired }]}
          extra={`${PURCHASE_TEXT.detail.total}: ${formatCurrency(order.totalAmount)}`}
        >
          <InputNumber min={0} precision={0} addonAfter="₫" style={{ width: 240 }} />
        </Form.Item>
        {differs && <Alert type="warning" showIcon message={text.differsFromTotal} style={{ marginBottom: 16 }} />}
        <Form.Item name="paymentMethod" label={text.method} rules={[{ required: true, message: text.methodRequired }]}>
          <Select options={PAYMENT_METHOD_OPTIONS} style={{ width: 240 }} />
        </Form.Item>
        <Form.Item name="paymentNote" label={text.note}>
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
