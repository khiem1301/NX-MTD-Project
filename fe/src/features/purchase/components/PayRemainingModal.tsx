import { Alert, Descriptions, Form, Input, InputNumber, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MESSAGES } from '@/constants/messages';
import { fill, formatCurrency, toApiDate } from '@/utils/format';
import { PAYMENT_METHOD_OPTIONS, PURCHASE_TEXT } from '../constants';
import { usePayRemaining } from '../hooks/usePurchase';
import type { PaymentMethod, PurchaseOrderDetail } from '../types';

type Props = { order: PurchaseOrderDetail; onClose: () => void };

type FormValues = {
  paymentDate: Dayjs;
  amount: number | null;
  paymentMethod: PaymentMethod;
  paymentNote: string | null;
};

const text = PURCHASE_TEXT.payRemaining;

/**
 * DEV-ALL-131: Kế toán trả thêm cho đơn Thanh toán một phần. Số tiền mặc định = còn phải trả, không được vượt;
 * trả đủ thì đơn thành Đã thanh toán. Trang chỉ render khi mở.
 */
export function PayRemainingModal({ order, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const pay = usePayRemaining(order.purchaseOrderId);
  const remaining = order.remainingAmount ?? 0;
  const paid = order.paidAmount ?? 0;
  const amount = Form.useWatch('amount', form);
  const valid = typeof amount === 'number' && amount > 0 && amount <= remaining;

  const handleFinish = (values: FormValues) =>
    pay.mutate(
      {
        paymentDate: toApiDate(values.paymentDate),
        amount: values.amount ?? 0,
        paymentMethod: values.paymentMethod,
        paymentNote: values.paymentNote?.trim() || null,
        rowVer: order.rowVer,
      },
      { onSuccess: onClose },
    );

  return (
    <Modal
      open
      title={`${text.title} · ${order.poCode}`}
      okText={PURCHASE_TEXT.actions.payRemaining}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={pay.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Descriptions bordered size="small" column={1} style={{ marginBottom: 16 }}>
        <Descriptions.Item label={text.target}>
          {formatCurrency(paid + remaining)}
        </Descriptions.Item>
        <Descriptions.Item label={text.paid}>{formatCurrency(paid)}</Descriptions.Item>
        <Descriptions.Item label={text.remaining}>{formatCurrency(remaining)}</Descriptions.Item>
      </Descriptions>
      <Form
        form={form}
        layout="vertical"
        disabled={pay.isPending}
        initialValues={{ paymentDate: dayjs(), amount: remaining, paymentMethod: 'BankTransfer' }}
        onFinish={handleFinish}
      >
        <Form.Item
          name="paymentDate"
          label={PURCHASE_TEXT.pay.date}
          rules={[{ required: true, message: PURCHASE_TEXT.pay.dateRequired }]}
        >
          <AppDatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item
          name="amount"
          label={text.amount}
          rules={[
            { required: true, type: 'number', min: 1, message: PURCHASE_TEXT.pay.amountRequired },
            {
              validator: async (_, value) => {
                if (typeof value === 'number' && value > remaining)
                  throw new Error(fill(text.amountExceeds, formatCurrency(remaining)));
              },
            },
          ]}
        >
          <InputNumber min={0} precision={0} addonAfter="₫" style={{ width: 240 }} />
        </Form.Item>
        {valid && (
          <Alert
            type={amount >= remaining ? 'success' : 'warning'}
            showIcon
            message={amount >= remaining ? text.willBePaid : text.willBePartial}
            style={{ marginBottom: 16 }}
          />
        )}
        <Form.Item
          name="paymentMethod"
          label={PURCHASE_TEXT.pay.method}
          rules={[{ required: true, message: PURCHASE_TEXT.pay.methodRequired }]}
        >
          <Select options={PAYMENT_METHOD_OPTIONS} style={{ width: 240 }} />
        </Form.Item>
        <Form.Item name="paymentNote" label={PURCHASE_TEXT.pay.note}>
          <Input.TextArea rows={2} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
