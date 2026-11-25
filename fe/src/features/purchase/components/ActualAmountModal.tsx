import { confirmAmount } from '@/utils/confirmAmount';
import { useEffect } from 'react';
import { Form, Input, Modal, Typography } from 'antd';
import { HintTip } from '@/components/common/HintTip';
import { MoneyInput } from '@/components/common/MoneyInput';
import { REASON_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { fill, formatCurrency } from '@/utils/format';
import { PURCHASE_TEXT } from '../constants';
import { useSetActualAmount } from '../hooks/usePurchase';
import type { PurchaseOrderDetail } from '../types';

type Props = { open: boolean; order: PurchaseOrderDetail; onClose: () => void };
type FormValues = { amount: number | null; reason?: string };

const text = PURCHASE_TEXT.actual;

/**
 * DEV-ALL-185: Kế toán nhập giá trị thực tế của đơn Chốt không bù ở nút riêng. Nhập một lần; sau lần nhập đầu tiên chỉ sửa được trong
 * 24 giờ và bắt buộc lý do. Chưa nhập thì gợi ý theo số đã nhận.
 */
export function ActualAmountModal({ open, order, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const save = useSetActualAmount(order.purchaseOrderId);
  const alreadyPaid = order.paidAmount ?? 0;
  // Có thời điểm nhập mới là "đã nhập"; số hệ thống tự tính theo số đã nhận vẫn được coi là lần nhập đầu của Kế toán.
  const editing = order.actualAmountSetAt !== null;

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    const suggested = order.actualAmount ?? order.receivedAmount;
    form.setFieldsValue({ amount: suggested > 0 ? Math.round(suggested) : null });
  }, [open, order, form]);

  const handleFinish = (values: FormValues) => {
    confirmAmount(text.amount, values.amount ?? 0, () => {
      save.mutate(
        {
          amount: values.amount ?? 0,
          reason: values.reason?.trim() || undefined,
          rowVer: order.rowVer,
        },
        { onSuccess: onClose },
      );
    });
  };

  return (
    <Modal
      open={open}
      title={`${editing ? PURCHASE_TEXT.actions.editActualAmount : PURCHASE_TEXT.actions.setActualAmount} · ${order.poCode}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <HintTip text={text.hint} />
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Typography.Paragraph type="secondary">
          {fill(
            text.suggested,
            formatCurrency(order.receivedAmount),
            formatCurrency(order.totalAmount),
          )}
          {alreadyPaid > 0 && ` ${text.paid}: ${formatCurrency(alreadyPaid)}.`}
        </Typography.Paragraph>
        <Form.Item
          name="amount"
          label={text.amount}
          validateTrigger="onSubmit"
          rules={[
            { required: true, type: 'number', min: 0.01, message: text.amountRequired },
            {
              validator: async (_, value) => {
                if (typeof value !== 'number') return;
                if (value > order.totalAmount) throw new Error(text.exceedsTotal);
                if (value < alreadyPaid) throw new Error(text.belowPaid);
              },
            },
          ]}
        >
          <MoneyInput style={{ width: 260 }} />
        </Form.Item>
        {editing && (
          <Form.Item
            name="reason"
            label={text.reason}
            rules={[{ required: true, whitespace: true, message: text.reasonRequired }]}
          >
            <Input.TextArea rows={2} maxLength={REASON_MAX_LENGTH} showCount />
          </Form.Item>
        )}
      </Form>
    </Modal>
  );
}
