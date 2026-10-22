import { useEffect } from 'react';
import { Checkbox, Form, Input, InputNumber, Modal } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { PRODUCTION_TEXT } from '../constants';
import { useSaveOperation } from '../hooks/useProduction';
import type { Operation } from '../types';

type Props = { open: boolean; productId: number; operation: Operation | null; onClose: () => void };

type FormValues = {
  operationCode: string;
  operationName: string;
  stationSequence: number;
  skillLevel?: number | null;
  standardTimeSec?: number | null;
  isFinalStep: boolean;
};

const text = PRODUCTION_TEXT.operations;
const required = [{ required: true, whitespace: true, message: PRODUCTION_TEXT.required }];

/** UC25 — Kế toán sửa một công đoạn đã nhập từ Excel (vd. đánh dấu công đoạn cuối). Mỗi mã hàng đúng một công đoạn cuối đang dùng (BE kiểm). */
export function OperationFormModal({ open, productId, operation, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveOperation(operation?.operationId ?? null);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue(
      operation
        ? {
            operationCode: operation.operationCode,
            operationName: operation.operationName,
            stationSequence: operation.stationSequence,
            skillLevel: operation.skillLevel,
            standardTimeSec: operation.standardTimeSec,
            isFinalStep: operation.isFinalStep,
          }
        : { isFinalStep: false },
    );
  }, [open, operation, form]);

  const handleFinish = (values: FormValues) => {
    save.mutate(
      {
        productId,
        operationCode: values.operationCode.trim(),
        operationName: values.operationName.trim(),
        stationSequence: values.stationSequence,
        skillLevel: values.skillLevel ?? null,
        standardTimeSec: values.standardTimeSec ?? null,
        teamType: 'Sewing',
        isFinalStep: values.isFinalStep,
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={520}
      title={text.edit}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={save.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={save.isPending}>
        <Form.Item name="operationCode" label={text.form.code} rules={required}>
          <Input maxLength={30} />
        </Form.Item>
        <Form.Item name="operationName" label={text.form.name} rules={required}>
          <Input maxLength={200} />
        </Form.Item>
        <Form.Item name="stationSequence" label={text.form.sequence} rules={[{ required: true, message: PRODUCTION_TEXT.required }]}>
          <InputNumber min={1} precision={0} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="skillLevel" label={text.form.skill}>
          <InputNumber min={1} max={9} precision={0} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="standardTimeSec" label={text.form.standardTime}>
          <InputNumber min={0.01} precision={2} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="isFinalStep" valuePropName="checked">
          <Checkbox>{text.form.isFinal}</Checkbox>
        </Form.Item>
      </Form>
    </Modal>
  );
}
