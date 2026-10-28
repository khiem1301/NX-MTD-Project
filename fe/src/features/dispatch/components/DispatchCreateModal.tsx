import { useState } from 'react';
import { DatePicker, Form, Input, InputNumber, Modal, Space, Table } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { DISPATCH_LABELS, DISPATCH_MESSAGES } from '../constants';
import { useCreateDispatch, useDispatchable } from '../hooks/useDispatch';
import type { DispatchableLine } from '../types';

type DispatchCreateModalProps = { open: boolean; onClose: () => void };
type FormValues = {
  deliveryDate: Dayjs;
  deliveryAddress: string;
  note?: string;
  qty: Record<string, number | null>;
};

const API_DATE = 'YYYY-MM-DD';

/** UC45: Quản đốc lập phiếu xuất hàng kiêm lệnh giao; thành phẩm được giữ chỗ ngay khi lập. */
export function DispatchCreateModal({ open, onClose }: DispatchCreateModalProps) {
  const [form] = Form.useForm<FormValues>();
  const [orderId, setOrderId] = useState<number>();
  const lines = useDispatchable(orderId);
  const create = useCreateDispatch();
  const required = [{ required: true, whitespace: true, message: MESSAGES.common.required }];

  const handleFinish = (values: FormValues) => {
    if (orderId === undefined) return;
    const payload = Object.entries(values.qty ?? {})
      .filter(([, q]) => (q ?? 0) > 0)
      .map(([id, q]) => ({ orderLineId: Number(id), issueQty: q ?? 0 }));
    create.mutate(
      {
        orderId,
        deliveryDate: values.deliveryDate.format(API_DATE),
        deliveryAddress: values.deliveryAddress.trim(),
        note: values.note,
        lines: payload,
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={820}
      title={DISPATCH_LABELS.create}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      okButtonProps={{ disabled: orderId === undefined || (lines.data ?? []).length === 0 }}
      onOk={() =>
        Modal.confirm({ title: DISPATCH_MESSAGES.confirmCreate, onOk: () => form.submit() })
      }
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        onFinish={handleFinish}
        disabled={create.isPending}
        initialValues={{ deliveryDate: dayjs().add(1, 'day') }}
      >
        <Space wrap align="start">
          <Form.Item label={DISPATCH_LABELS.order} required>
            <OrderSelect
              value={orderId}
              placeholder={DISPATCH_LABELS.selectOrder}
              onChange={(id) => {
                setOrderId(id);
                form.setFieldValue('qty', {});
              }}
            />
          </Form.Item>
          <Form.Item name="deliveryDate" label={DISPATCH_LABELS.deliveryDate} rules={required}>
            <DatePicker format="DD/MM/YYYY" />
          </Form.Item>
        </Space>
        <Form.Item name="deliveryAddress" label={DISPATCH_LABELS.deliveryAddress} rules={required}>
          <Input maxLength={500} />
        </Form.Item>
        <Form.Item name="note" label={DISPATCH_LABELS.note}>
          <Input.TextArea rows={2} maxLength={1000} />
        </Form.Item>
        <Table<DispatchableLine>
          rowKey="orderLineId"
          size="small"
          pagination={false}
          loading={lines.isFetching}
          dataSource={lines.data}
          columns={[
            {
              title: DISPATCH_LABELS.orderLine,
              key: 'line',
              render: (_, l) => `${l.productCode} - ${l.color} - ${l.sizeCode}`,
            },
            { title: DISPATCH_LABELS.orderedQty, dataIndex: 'orderedQty', align: 'right' },
            { title: DISPATCH_LABELS.issuedQty, dataIndex: 'issuedQty', align: 'right' },
            { title: DISPATCH_LABELS.remainingQty, dataIndex: 'remainingQty', align: 'right' },
            {
              title: DISPATCH_LABELS.availableQty,
              dataIndex: 'availableQty',
              align: 'right',
              render: formatQty,
            },
            {
              title: DISPATCH_LABELS.exportQty,
              key: 'qty',
              render: (_, l) => {
                const max = Math.min(l.remainingQty, Math.floor(l.availableQty));
                return (
                  <Form.Item name={['qty', String(l.orderLineId)]} noStyle>
                    <InputNumber min={0} max={max} precision={0} disabled={max <= 0} />
                  </Form.Item>
                );
              },
            },
          ]}
        />
      </Form>
    </Modal>
  );
}
