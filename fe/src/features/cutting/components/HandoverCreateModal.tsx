import { useEffect } from 'react';
import { Form, InputNumber, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { LABELS, lineKey, parseLineKey } from '../constants';
import { useCreateHandover, useHandoverable } from '../hooks/useCutting';
import type { HandoverableLine } from '../types';
import { confirmDialog } from '@/utils/confirmDialog';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { DataTable } from '@/components/common/DataTable';

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
      qty: Object.fromEntries(handoverable.data.map((l) => [lineKey(l), l.remainingQty])),
    });
  }, [handoverable.data, form]);

  const handleFinish = ({ qty }: FormValues) => {
    const lines = Object.entries(qty ?? {})
      .filter(([, q]) => (q ?? 0) > 0)
      .map(([key, q]) => ({ ...parseLineKey(key), handedQty: q ?? 0 }));
    create.mutate({ workOrderId, lines }, { onSuccess: onClose });
  };

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={LABELS.createHandover}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() =>
        confirmDialog({ title: MESSAGES.cutting.confirmHandoverCreate, onOk: () => form.submit() })
      }
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} onFinish={handleFinish} disabled={create.isPending}>
        <DataTable<HandoverableLine>
          rowKey={lineKey}
          size="small"
          pagination={false}
          loading={handoverable.isFetching}
          dataSource={handoverable.data}
          columns={[
            {
              title: LABELS.orderLine,
              key: 'line',
              render: (_, l) =>
                `${l.color} - ${l.sizeCode}${l.componentName ? ` · ${l.componentName}` : ''}`,
            },
            { title: LABELS.cutQty, dataIndex: 'cutQty', align: 'right' },
            { title: LABELS.handedQty, dataIndex: 'handedQty', align: 'right' },
            { title: LABELS.remainingQty, dataIndex: 'remainingQty', align: 'right' },
            {
              title: LABELS.handoverQty,
              key: 'qty',
              render: (_, l) => (
                <Form.Item name={['qty', lineKey(l)]} noStyle>
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
