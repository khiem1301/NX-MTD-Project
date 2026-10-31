import { useMemo, useState } from 'react';
import { Alert, Form, Input, InputNumber, Modal, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { formatQty } from '@/utils/format';
import { RETURN_LABELS, RETURN_MESSAGES } from '../constants';
import { useCreateReturn, useReturnable } from '../hooks/useMaterialReturn';
import type { ReturnableLine } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { DataTable } from '@/components/common/DataTable';

type ReturnCreateModalProps = { open: boolean; onClose: () => void };
type FormValues = { qty: Record<string, number | null>; note?: string };

/** UC42: tổ cắt chọn phiếu cấp đã nhận rồi nhập số vải thừa trả lại (≤ số đã nhận chưa trả). */
export function ReturnCreateModal({ open, onClose }: ReturnCreateModalProps) {
  const [form] = Form.useForm<FormValues>();
  const returnable = useReturnable(open);
  const create = useCreateReturn();
  const [issueId, setIssueId] = useState<number>();

  const issues = useMemo(() => {
    const seen = new Map<number, string>();
    (returnable.data ?? []).forEach((l) =>
      seen.set(l.materialIssueId, `${l.issueCode} · ${l.orderCode}`),
    );
    return [...seen].map(([value, label]) => ({ value, label }));
  }, [returnable.data]);
  const lines = (returnable.data ?? []).filter((l) => l.materialIssueId === issueId);

  const handleFinish = ({ qty, note }: FormValues) => {
    if (issueId === undefined) return;
    const payload = Object.entries(qty ?? {})
      .filter(([, q]) => (q ?? 0) > 0)
      .map(([id, q]) => ({ materialIssueLineId: Number(id), returnedQty: q ?? 0 }));
    create.mutate({ materialIssueId: issueId, note, lines: payload }, { onSuccess: onClose });
  };

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.md}
      title={RETURN_LABELS.create}
      okText={MESSAGES.common.confirm}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      okButtonProps={{ disabled: lines.length === 0 }}
      onOk={() =>
        Modal.confirm({ title: RETURN_MESSAGES.confirmCreate, onOk: () => form.submit() })
      }
      onCancel={onClose}
      destroyOnHidden
    >
      {!returnable.isLoading && issues.length === 0 && (
        <Alert
          type="info"
          showIcon
          message={RETURN_LABELS.nothingToReturn}
          style={{ marginBottom: 12 }}
        />
      )}
      <Select
        value={issueId}
        options={issues}
        placeholder={RETURN_LABELS.issue}
        loading={returnable.isLoading}
        style={{ width: 360, marginBottom: 16 }}
        onChange={(id: number) => {
          setIssueId(id);
          form.resetFields();
        }}
      />
      <Form
        form={form}
        layout="vertical"
        disabled={create.isPending}
        onFinish={handleFinish}
        key={issueId}
      >
        <DataTable<ReturnableLine>
          rowKey="materialIssueLineId"
          size="small"
          pagination={false}
          dataSource={lines}
          columns={[
            {
              title: RETURN_LABELS.material,
              key: 'material',
              render: (_, l) =>
                `${l.materialCode} ${l.materialName}${l.materialColor ? ` - ${l.materialColor}` : ''}`,
            },
            { title: RETURN_LABELS.unit, dataIndex: 'unit' },
            {
              title: RETURN_LABELS.receivedQty,
              dataIndex: 'receivedQty',
              align: 'right',
              render: formatQty,
            },
            {
              title: RETURN_LABELS.returnedQty,
              dataIndex: 'returnedQty',
              align: 'right',
              render: formatQty,
            },
            {
              title: RETURN_LABELS.remainingQty,
              dataIndex: 'remainingQty',
              align: 'right',
              render: formatQty,
            },
            {
              title: RETURN_LABELS.returnQty,
              key: 'qty',
              render: (_, l) => (
                <Form.Item name={['qty', String(l.materialIssueLineId)]} noStyle>
                  <InputNumber min={0} max={l.remainingQty} />
                </Form.Item>
              ),
            },
          ]}
        />
        <Form.Item name="note" label={RETURN_LABELS.note} style={{ marginTop: 16 }}>
          <Input.TextArea rows={2} maxLength={500} />
        </Form.Item>
      </Form>
    </Modal>
  );
}
