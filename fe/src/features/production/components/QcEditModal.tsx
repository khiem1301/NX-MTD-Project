import { useEffect } from 'react';
import { Form, Input, InputNumber, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useUpdateQcBatch } from '../hooks/useProduction';
import type { QcBatch, QcRole } from '../types';
import { PRODUCTION_TEXT } from '../constants';

const text = PRODUCTION_TEXT.qc;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

type EditValues = { finishedQty: number; reason?: string };

/** Sửa số hàng đạt của một đợt. Sau hết ca chỉ Quản đốc sửa và phải ghi lý do (BE kiểm). */
export function QcEditModal({
  role,
  batch,
  onClose,
}: {
  role: QcRole;
  batch: QcBatch | null;
  onClose: () => void;
}) {
  const [form] = Form.useForm<EditValues>();
  const update = useUpdateQcBatch(role);
  const needReason = role === 'manager' || (batch !== null && !batch.canEdit);

  useEffect(() => {
    if (batch) form.setFieldsValue({ finishedQty: batch.finishedQty, reason: undefined });
  }, [batch, form]);

  return (
    <Modal
      open={batch !== null}
      title={text.editTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        onFinish={(values) =>
          batch &&
          update.mutate(
            {
              id: batch.qcBatchId,
              body: {
                finishedQty: values.finishedQty,
                reason: values.reason?.trim() || undefined,
                rowVer: batch.rowVer,
              },
            },
            { onSuccess: onClose },
          )
        }
      >
        <Form.Item
          name="finishedQty"
          label={text.form.qty}
          rules={[...required, { type: 'number', min: 1, message: text.form.qtyMin }]}
        >
          <InputNumber min={1} precision={0} style={{ width: 180 }} />
        </Form.Item>
        {needReason && (
          <Form.Item
            name="reason"
            label={text.form.reason}
            rules={[{ required: true, whitespace: true, message: MESSAGES.common.reasonRequired }]}
          >
            <Input.TextArea rows={2} maxLength={500} showCount />
          </Form.Item>
        )}
      </Form>
    </Modal>
  );
}
