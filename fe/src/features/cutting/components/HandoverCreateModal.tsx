import { useEffect } from 'react';
import { Form, InputNumber, Modal, Table } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { LABELS } from '../constants';
import { useCreateHandover, useHandoverable } from '../hooks/useCutting';
import type { HandoverableLine } from '../types';

type HandoverCreateModalProps = { open: boolean; workOrderId: number; onClose: () => void };
type FormValues = { qty: Record<string, number | null> };

/** UC43: tổ cắt bàn giao BTP đã cắt vào kho; mặc định giao hết số còn lại. Lập xong các bàn cắt bị khóa. */
export function HandoverCreateModal({ open, workOrderId, onClose }: HandoverCreateModalProps) {
  const [form] = Form.useForm<FormValues>();
  const handoverable = useHandoverable(open ? workOrderId : undefined);
  const create = useCreateHandover();

  useEffect(() => {
    if (!handoverable.data) return;
    form.setFieldsValue({
      qty: Object.fromEntries(
        handoverable.data.map((l) => [String(l.orderLineId), l.remainingQty]),
      ),
    });
  }, [handoverable.data, form]);

  const handleFinish = ({ qty }: FormValues) => {
    const lines = Object.entries(qty ?? {})
      .filter(([, q]) => (q ?? 0) > 0)
      .map(([id, q]) => ({ orderLineId: Number(id), handedQty: q ?? 0 }));
    create.mutate({ workOrderId, lines }, { onSuccess: onClose });
  };

  return (
    <Modal
      open={open}
      width={720}
      title={LABELS.createHandover}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() =>
        Modal.confirm({ title: MESSAGES.cutting.confirmHandoverCreate, onOk: () => form.submit() })
      }
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} onFinish={handleFinish} disabled={create.isPending}>
        <Table<HandoverableLine>
          rowKey="orderLineId"
          size="small"
          pagination={false}
          loading={handoverable.isFetching}
          dataSource={handoverable.data}
          columns={[
            {
              title: LABELS.orderLine,
              key: 'line',
              render: (_, l) => `${l.color} - ${l.sizeCode}`,
            },
            { title: LABELS.cutQty, dataIndex: 'cutQty', align: 'right' },
            { title: LABELS.handedQty, dataIndex: 'handedQty', align: 'right' },
            { title: LABELS.remainingQty, dataIndex: 'remainingQty', align: 'right' },
            {
              title: LABELS.handoverQty,
              key: 'qty',
              render: (_, l) => (
                <Form.Item name={['qty', String(l.orderLineId)]} noStyle>
                  <InputNumber min={0} max={l.remainingQty} precision={0} />
                </Form.Item>
              ),
            },
          ]}
        />
      </Form>
    </Modal>
  );
}
