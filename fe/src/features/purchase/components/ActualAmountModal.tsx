import { useEffect } from 'react';
import { Alert, Button, Form, Input, InputNumber, Modal, Space } from 'antd';
import { REASON_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency } from '@/utils/format';
import { PURCHASE_TEXT } from '../constants';
import { useSetActualAmount } from '../hooks/usePurchase';
import type { PurchaseOrderDetail } from '../types';

type Props = { open: boolean; order: PurchaseOrderDetail; onClose: () => void };
type FormValues = { amount: number | null; reason: string };

const text = PURCHASE_TEXT.actual;

/**
 * Kế toán nhập giá trị thực tế của đơn đang nhận hàng / đã chốt giao thiếu (khác giá trị tự tính), bắt buộc lý do.
 * Không nhỏ hơn số đã thanh toán. "Bỏ giá trị đã nhập" trả về giá trị tự tính.
 */
export function ActualAmountModal({ open, order, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const save = useSetActualAmount(order.purchaseOrderId);
  const paid = order.paidAmount ?? 0;

  useEffect(() => {
    if (!open) return;
    form.setFieldsValue({
      amount: order.actualAmountOverride ?? order.actualAmount,
      reason: order.actualAmountReason ?? '',
    });
  }, [open, order, form]);

  const send = (amount: number | null, reason: string | null) =>
    save.mutate({ amount, reason, rowVer: order.rowVer }, { onSuccess: onClose });

  return (
    <Modal
      open={open}
      title={`${text.title} · ${order.poCode}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Alert type="info" showIcon message={text.hint} style={{ marginBottom: 16 }} />
      <Form
        form={form}
        layout="vertical"
        disabled={save.isPending}
        onFinish={(v) => send(v.amount ?? 0, v.reason.trim())}
      >
        <Form.Item
          name="amount"
          label={text.amount}
          validateTrigger="onSubmit"
          rules={[
            { required: true, type: 'number', min: 0, message: text.amountRequired },
            {
              validator: async (_, value) => {
                if (typeof value !== 'number' || value >= paid) return;
                throw new Error(text.belowPaid);
              },
            },
          ]}
          extra={`${text.computed}: ${formatCurrency(order.totalAmount)} · ${text.paid}: ${formatCurrency(paid)}`}
        >
          <InputNumber min={0} precision={0} addonAfter="₫" style={{ width: 240 }} />
        </Form.Item>
        <Form.Item
          name="reason"
          label={text.reason}
          rules={[{ required: true, whitespace: true, message: text.reasonRequired }]}
        >
          <Input.TextArea rows={2} maxLength={REASON_MAX_LENGTH} showCount />
        </Form.Item>
      </Form>
      {order.actualAmountOverride !== null && (
        <Space>
          <Button danger loading={save.isPending} onClick={() => send(null, null)}>
            {text.clear}
          </Button>
        </Space>
      )}
    </Modal>
  );
}
