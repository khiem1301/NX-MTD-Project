import { useEffect } from 'react';
import { Alert, DatePicker, Form, Input, InputNumber, Modal, Select, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { formatCurrency, formatQty, toApiDate } from '@/utils/format';
import { PURCHASE_TEXT } from '../constants';
import { usePlaceOrder, useSupplierOptions } from '../hooks/usePurchase';
import type { PurchaseOrderDetail } from '../types';

type Props = { open: boolean; order: PurchaseOrderDetail; onClose: () => void };

type LineValues = { purchaseOrderLineId: number; orderedQty: number | null; unitPrice: number | null };

type FormValues = {
  supplierId: number;
  orderDate: Dayjs;
  expectedDeliveryCount: number;
  note: string | null;
  lines: LineValues[];
};

const DATE_FORMAT = 'DD/MM/YYYY';

/** UC23: Quản đốc chọn nhà cung cấp đang hợp tác, chốt số lượng + đơn giá rồi đặt hàng (sau đó khóa — GBR-18). */
export function PlaceOrderModal({ open, order, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const place = usePlaceOrder(order.purchaseOrderId);
  const suppliers = useSupplierOptions(open);
  const text = PURCHASE_TEXT.place;
  const lines = Form.useWatch('lines', form) ?? [];
  const total = lines.reduce((sum, l) => sum + (l?.orderedQty ?? 0) * (l?.unitPrice ?? 0), 0);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({
      orderDate: dayjs(),
      expectedDeliveryCount: 1,
      note: order.note,
      lines: order.lines.map((l) => ({
        purchaseOrderLineId: l.purchaseOrderLineId,
        orderedQty: l.orderedQty,
        unitPrice: l.unitPrice > 0 ? l.unitPrice : null,
      })),
    });
  }, [open, order, form]);

  const handleFinish = (values: FormValues) => {
    place.mutate(
      {
        supplierId: values.supplierId,
        orderDate: toApiDate(values.orderDate),
        expectedDeliveryCount: values.expectedDeliveryCount,
        note: values.note?.trim() || null,
        lines: values.lines.map((l) => ({
          purchaseOrderLineId: l.purchaseOrderLineId,
          orderedQty: l.orderedQty ?? 0,
          unitPrice: l.unitPrice ?? 0,
        })),
        rowVer: order.rowVer,
      },
      { onSuccess: onClose },
    );
  };

  const options = (suppliers.data ?? []).map((s) => ({ value: s.supplierId, label: s.supplierName }));

  return (
    <Modal
      open={open}
      width={760}
      title={`${text.title} · ${order.poCode}`}
      okText={PURCHASE_TEXT.actions.place}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={place.isPending}
      okButtonProps={{ disabled: options.length === 0 }}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      {suppliers.isSuccess && options.length === 0 ? (
        <Alert type="warning" showIcon message={text.noSupplier} style={{ marginBottom: 16 }} />
      ) : (
        <Alert type="info" showIcon message={text.lockedHint} style={{ marginBottom: 16 }} />
      )}
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={place.isPending}>
        <Space wrap align="start">
          <Form.Item
            name="supplierId"
            label={text.supplier}
            rules={[{ required: true, message: text.supplierRequired }]}
          >
            <Select
              showSearch
              optionFilterProp="label"
              options={options}
              loading={suppliers.isLoading}
              style={{ width: 280 }}
            />
          </Form.Item>
          <Form.Item
            name="orderDate"
            label={text.orderDate}
            rules={[{ required: true, message: text.orderDateRequired }]}
          >
            <DatePicker format={DATE_FORMAT} disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item
            name="expectedDeliveryCount"
            label={text.expectedDate}
            rules={[{ required: true, type: 'number', min: 1, message: text.expectedDateRequired }]}
          >
            <InputNumber min={1} max={99} precision={0} style={{ width: 150 }} />
          </Form.Item>
        </Space>

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
                  <Form.Item
                    name={[field.name, 'orderedQty']}
                    rules={[{ required: true, type: 'number', min: 0.001, message: text.qtyRequired }]}
                  >
                    <InputNumber min={0} precision={3} addonAfter={line?.unit} style={{ width: 170 }} />
                  </Form.Item>
                  <Form.Item
                    name={[field.name, 'unitPrice']}
                    rules={[{ required: true, type: 'number', min: 0.01, message: text.priceRequired }]}
                  >
                    <InputNumber
                      min={0}
                      precision={0}
                      addonAfter="₫"
                      placeholder={PURCHASE_TEXT.detail.unitPrice}
                      style={{ width: 170 }}
                    />
                  </Form.Item>
                </Space>
              );
            })
          }
        </Form.List>

        <Form.Item name="note" label={text.note}>
          <Input.TextArea rows={2} maxLength={1000} showCount />
        </Form.Item>
        <Typography.Text strong>{`${PURCHASE_TEXT.detail.total}: ${formatCurrency(total)}`}</Typography.Text>
        <Typography.Text type="secondary" style={{ marginLeft: 12 }}>
          {order.lines.length > 0 ? `(${formatQty(order.lines.length)} dòng)` : ''}
        </Typography.Text>
      </Form>
    </Modal>
  );
}
