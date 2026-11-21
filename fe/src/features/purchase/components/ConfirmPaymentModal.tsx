import { useEffect } from 'react';
import { Form, Input, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { toApiDate } from '@/utils/format';
import { PAYMENT_METHOD_OPTIONS, PURCHASE_TEXT } from '../constants';
import { useConfirmPayment } from '../hooks/usePurchase';
import type { PaymentMethod, PurchaseOrderDetail } from '../types';
import { AppDatePicker } from '@/components/common/DateInputs';
import { PaymentAmountFields } from './PaymentAmountFields';

type Props = { open: boolean; order: PurchaseOrderDetail; onClose: () => void };

type FormValues = {
  paymentDate: Dayjs;
  paidAmount: number | null;
  actualAmount?: number | null;
  paymentMethod: PaymentMethod;
  paymentNote: string | null;
};

const positiveOrNull = (value: number) => (value > 0 ? Math.round(value) : null);

/**
 * UC34: Kế toán xác nhận thanh toán đơn mua. Ảnh chứng từ không bắt buộc, đính ở khung ảnh bên ngoài.
 * Đơn Chốt không bù: nhập Tổng giá trị thực tế (gợi ý theo số đã nhận) — DEV-ALL-129.
 */
export function ConfirmPaymentModal({ open, order, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const confirm = useConfirmPayment(order.purchaseOrderId);
  const text = PURCHASE_TEXT.pay;
  const closedShort = order.goodsStatus === 'ClosedShort';

  useEffect(() => {
    if (!open) return;
    const actual = closedShort ? positiveOrNull(order.actualAmount ?? order.receivedAmount) : null;
    form.resetFields();
    form.setFieldsValue({
      paymentDate: dayjs(),
      actualAmount: actual,
      paidAmount: positiveOrNull(
        (closedShort ? (actual ?? 0) : order.totalAmount) - (order.paidAmount ?? 0),
      ),
      paymentMethod: 'BankTransfer',
    });
  }, [open, order, form, closedShort]);

  const handleFinish = (values: FormValues) => {
    confirm.mutate(
      {
        paymentDate: toApiDate(values.paymentDate),
        paidAmount: values.paidAmount ?? 0,
        paymentMethod: values.paymentMethod,
        paymentNote: values.paymentNote?.trim() || null,
        rowVer: order.rowVer,
        actualAmount: closedShort ? (values.actualAmount ?? null) : null,
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
        <Form.Item
          name="paymentDate"
          label={text.date}
          rules={[{ required: true, message: text.dateRequired }]}
        >
          <AppDatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <PaymentAmountFields order={order} form={form} closedShort={closedShort} />
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
      </Form>
    </Modal>
  );
}
