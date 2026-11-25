import { useEffect } from 'react';
import { Form, Input, Modal, Space, Typography } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { AppDatePicker } from '@/components/common/DateInputs';
import { DeliveryDateFields } from '@/components/common/DeliveryDateFields';
import { HintTip } from '@/components/common/HintTip';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { QtyInput } from '@/components/common/QtyInput';
import { REASON_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';
import { confirmDialog } from '@/utils/confirmDialog';
import { toDeliveryDatesPayload } from '@/utils/expectedDelivery';
import { formatCurrency, toApiDate } from '@/utils/format';
import { PURCHASE_TEXT } from '../constants';
import { useEditOrder } from '../hooks/usePurchase';
import type { PurchaseOrderDetail } from '../types';
import { PlaceOrderLines } from './PlaceOrderLines';

type Props = { open: boolean; order: PurchaseOrderDetail; onClose: () => void };

type FormValues = {
  orderDate: Dayjs;
  expectedDeliveryCount: number;
  expectedDeliveryDates?: (Dayjs | null)[];
  note: string | null;
  reason: string;
  lines: { purchaseOrderLineId: number; orderedQty: number | null; unitPrice: number | null }[];
};

const text = PURCHASE_TEXT.edit;

/**
 * DEV-ALL-186: Quản đốc sửa đơn mua trong 24 giờ kể từ lúc tạo (chưa nhận hàng, chưa thanh toán): nhà cung cấp, ngày, số lần giao,
 * số lượng, đơn giá. Bắt buộc lý do; bấm lưu thì hỏi xác nhận lại tổng tiền.
 */
export function EditOrderModal({ open, order, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const edit = useEditOrder(order.purchaseOrderId);
  const lines = Form.useWatch('lines', form) ?? [];
  const orderDate = Form.useWatch('orderDate', form);
  const deliveryCount = Form.useWatch('expectedDeliveryCount', form);
  const total = lines.reduce((sum, l) => sum + (l?.orderedQty ?? 0) * (l?.unitPrice ?? 0), 0);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({
      orderDate: order.orderDate ? dayjs(order.orderDate) : dayjs(),
      expectedDeliveryCount: order.expectedDeliveryCount ?? 1,
      expectedDeliveryDates: (order.expectedDeliveryDates ?? []).map((d) => dayjs(d)),
      note: order.note,
      lines: order.lines.map((l) => ({
        purchaseOrderLineId: l.purchaseOrderLineId,
        orderedQty: l.orderedQty,
        unitPrice: l.unitPrice,
      })),
    });
  }, [open, order, form]);

  const submit = (values: FormValues) =>
    edit.mutate(
      {
        orderDate: toApiDate(values.orderDate),
        expectedDeliveryCount: values.expectedDeliveryCount,
        expectedDeliveryDates: toDeliveryDatesPayload(
          values.expectedDeliveryCount,
          values.expectedDeliveryDates,
        ),
        note: values.note?.trim() || null,
        reason: values.reason.trim(),
        lines: values.lines.map((l) => ({
          purchaseOrderLineId: l.purchaseOrderLineId,
          orderedQty: l.orderedQty ?? 0,
          unitPrice: l.unitPrice ?? 0,
        })),
      },
      { onSuccess: onClose },
    );

  const handleFinish = (values: FormValues) =>
    confirmDialog({
      title: text.confirmTitle,
      content: `${PURCHASE_TEXT.detail.total}: ${formatCurrency(total)}. ${text.confirmContent}`,
      onOk: () => submit(values),
    });

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={`${text.title} · ${order.poCode}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={edit.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <HintTip text={text.hint} />
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={edit.isPending}>
        <Space wrap align="start">
          <Form.Item label={PURCHASE_TEXT.place.supplier} extra={text.supplierLocked}>
            <Typography.Text strong>{order.supplierName ?? '—'}</Typography.Text>
          </Form.Item>
          <Form.Item
            name="orderDate"
            label={PURCHASE_TEXT.place.orderDate}
            rules={[{ required: true, message: PURCHASE_TEXT.place.orderDateRequired }]}
          >
            <AppDatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item
            name="expectedDeliveryCount"
            label={PURCHASE_TEXT.place.expectedDate}
            rules={[
              {
                required: true,
                type: 'number',
                min: 1,
                message: PURCHASE_TEXT.place.expectedDateRequired,
              },
            ]}
          >
            <QtyInput min={1} max={99} style={{ width: 150 }} />
          </Form.Item>
          <DeliveryDateFields
            count={deliveryCount}
            label={PURCHASE_TEXT.place.expectedDeliveryDate}
            requiredMessage={PURCHASE_TEXT.place.expectedDeliveryDateRequired}
            notBefore={orderDate}
          />
        </Space>
        <PlaceOrderLines orderLines={order.lines} />
        <Form.Item name="note" label={PURCHASE_TEXT.place.note}>
          <Input.TextArea rows={2} maxLength={1000} showCount />
        </Form.Item>
        <Form.Item
          name="reason"
          label={text.reason}
          rules={[{ required: true, whitespace: true, message: text.reasonRequired }]}
        >
          <Input.TextArea rows={2} maxLength={REASON_MAX_LENGTH} showCount />
        </Form.Item>
        <Typography.Text
          strong
        >{`${PURCHASE_TEXT.detail.total}: ${formatCurrency(total)}`}</Typography.Text>
      </Form>
    </Modal>
  );
}
