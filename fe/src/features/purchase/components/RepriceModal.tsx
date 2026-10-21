import { useEffect } from 'react';
import { Alert, Form, Input, InputNumber, Modal, Space, Typography } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatQty } from '@/utils/format';
import { PURCHASE_TEXT } from '../constants';
import { useRepriceOrder } from '../hooks/usePurchase';
import type { PurchaseOrderDetail } from '../types';

type Props = { open: boolean; order: PurchaseOrderDetail; onClose: () => void };

type FormValues = {
  note: string | null;
  lines: { purchaseOrderLineId: number; unitPrice: number | null }[];
};

/** Sau khi Kế toán trả lại: Quản đốc chỉ sửa đơn giá (số lượng, nhà cung cấp khóa theo GBR-18) rồi gửi thanh toán lại. */
export function RepriceModal({ open, order, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const reprice = useRepriceOrder(order.purchaseOrderId);
  const text = PURCHASE_TEXT.reprice;
  const lines = Form.useWatch('lines', form) ?? [];
  const total = lines.reduce((sum, l, i) => sum + (order.lines[i]?.orderedQty ?? 0) * (l?.unitPrice ?? 0), 0);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({
      note: order.note,
      lines: order.lines.map((l) => ({ purchaseOrderLineId: l.purchaseOrderLineId, unitPrice: l.unitPrice })),
    });
  }, [open, order, form]);

  const handleFinish = (values: FormValues) => {
    reprice.mutate(
      {
        note: values.note?.trim() || null,
        lines: values.lines.map((l) => ({ purchaseOrderLineId: l.purchaseOrderLineId, unitPrice: l.unitPrice ?? 0 })),
        rowVer: order.rowVer,
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={640}
      title={`${text.title} · ${order.poCode}`}
      okText={PURCHASE_TEXT.actions.reprice}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={reprice.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Alert type="info" showIcon message={text.hint} style={{ marginBottom: 12 }} />
      {order.paymentReturnReason && (
        <Alert
          type="warning"
          showIcon
          message={`${text.returnedBy}: ${order.paymentReturnReason}`}
          style={{ marginBottom: 16 }}
        />
      )}
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={reprice.isPending}>
        <Form.List name="lines">
          {(fields) =>
            fields.map((field, index) => {
              const line = order.lines[index];
              return (
                <Space key={field.key} align="baseline" wrap style={{ display: 'flex' }}>
                  <Form.Item name={[field.name, 'purchaseOrderLineId']} hidden>
                    <InputNumber />
                  </Form.Item>
                  <Typography.Text style={{ display: 'inline-block', width: 230 }}>
                    {line ? `${line.materialName}${line.materialColor ? ` (${line.materialColor})` : ''}` : ''}
                  </Typography.Text>
                  <Typography.Text type="secondary" style={{ display: 'inline-block', width: 110 }}>
                    {line ? `${formatQty(line.orderedQty)} ${line.unit}` : ''}
                  </Typography.Text>
                  <Form.Item
                    name={[field.name, 'unitPrice']}
                    rules={[{ required: true, type: 'number', min: 0.01, message: PURCHASE_TEXT.place.priceRequired }]}
                  >
                    <InputNumber min={0} precision={0} addonAfter="₫" style={{ width: 170 }} />
                  </Form.Item>
                </Space>
              );
            })
          }
        </Form.List>
        <Form.Item name="note" label={PURCHASE_TEXT.detail.note}>
          <Input.TextArea rows={2} maxLength={1000} showCount />
        </Form.Item>
        <Typography.Text strong>{`${PURCHASE_TEXT.detail.total}: ${formatCurrency(total)}`}</Typography.Text>
      </Form>
    </Modal>
  );
}
