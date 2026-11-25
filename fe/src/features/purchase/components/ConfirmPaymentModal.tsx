import { confirmAmount } from '@/utils/confirmAmount';
import { useEffect } from 'react';
import { Form, Input, Modal, Select, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { toApiDate } from '@/utils/format';
import { PAYMENT_METHOD_OPTIONS, PURCHASE_TEXT } from '../constants';
import { useConfirmPayment } from '../hooks/usePurchase';
import type { PaymentMethod, PurchaseOrderDetail } from '../types';
import { AppDatePicker } from '@/components/common/DateInputs';
import { PaymentAmountFields } from './PaymentAmountFields';
import { PaymentProofGallery } from './PaymentProofGallery';
import { newProofCount } from '../utils/paymentProofs';
import { useAttachments } from '@/hooks/useAttachments';
import { ATTACHMENT_OWNER } from '@/types/attachment';
import { fill } from '@/utils/format';
import { Alert } from 'antd';

type Props = { open: boolean; order: PurchaseOrderDetail; onClose: () => void };

type FormValues = {
  paymentDate: Dayjs;
  paidAmount: number | null;
  paymentMethod: PaymentMethod;
  paymentNote: string | null;
};

const positiveOrNull = (value: number) => (value > 0 ? Math.round(value) : null);

/**
 * UC34: Kế toán xác nhận thanh toán đơn mua. Ảnh chứng từ không bắt buộc, đính ở khung ảnh bên ngoài.
 * Đơn Chốt không bù: thanh toán theo giá trị thực tế đã nhập ở nút riêng — DEV-ALL-129, 185.
 */
export function ConfirmPaymentModal({ open, order, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const confirm = useConfirmPayment(order.purchaseOrderId);
  const text = PURCHASE_TEXT.pay;
  const closedShort = order.goodsStatus === 'ClosedShort';
  // DEV-ALL-189: mỗi lần thanh toán cần ít nhất một ảnh chứng từ tải lên sau lần thanh toán trước.
  const proofs = useAttachments(
    ATTACHMENT_OWNER.PurchaseOrder,
    open ? order.purchaseOrderId : null,
  );
  const newProofs = newProofCount(proofs.data ?? [], order.payments);

  useEffect(() => {
    if (!open) return;
    const target = closedShort ? (order.actualAmount ?? 0) : order.totalAmount;
    form.resetFields();
    form.setFieldsValue({
      paymentDate: dayjs(),
      paidAmount: positiveOrNull(target - (order.paidAmount ?? 0)),
      paymentMethod: 'BankTransfer',
    });
  }, [open, order, form, closedShort]);

  const handleFinish = (values: FormValues) => {
    confirmAmount(text.amount, values.paidAmount ?? 0, () => {
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
    });
  };

  return (
    <Modal
      open={open}
      title={`${text.title} · ${order.poCode}`}
      okText={PURCHASE_TEXT.actions.confirmPayment}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={confirm.isPending}
      okButtonProps={{
        disabled: (closedShort && order.actualAmountSetAt === null) || newProofs === 0,
      }}
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
      <Typography.Title level={5}>{text.proofTitle}</Typography.Title>
      <Alert
        type={newProofs > 0 ? 'success' : 'warning'}
        showIcon
        message={newProofs > 0 ? fill(text.proofNew, newProofs) : text.proofMissing}
        description={text.proofHint}
        style={{ marginBottom: 12 }}
      />
      <PaymentProofGallery purchaseOrderId={order.purchaseOrderId} canEdit />
    </Modal>
  );
}
