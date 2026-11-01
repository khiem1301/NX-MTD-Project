import { useEffect } from 'react';
import { AutoComplete, Checkbox, Form, Input, InputNumber, Modal, Select, Switch } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useProductSpec } from '@/features/orders';
import { PRODUCTION_TEXT } from '../constants';
import { useSaveOperation } from '../hooks/useProduction';
import type { Operation } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';

type Props = {
  open: boolean;
  productId: number;
  operation: Operation | null;
  /** Công đoạn to đã có của mã hàng (gợi ý khi gõ). */
  groups: string[];
  onClose: () => void;
  /** Gọi sau khi tạo mới công đoạn (không gọi khi sửa) — để mở tiếp ô nhập đơn giá. */
  onCreated?: (operationId: number) => void;
};

type FormValues = {
  operationCode: string;
  operationName: string;
  stationSequence: number;
  skillLevel?: number | null;
  standardTimeSec?: number | null;
  isFinalStep: boolean;
  usesSemiFinished: boolean;
  groupName?: string;
  componentIds: number[];
};

const text = PRODUCTION_TEXT.operations;
const required = [{ required: true, whitespace: true, message: PRODUCTION_TEXT.required }];

/** UC25 — Kế toán sửa một công đoạn đã nhập từ Excel (vd. đánh dấu công đoạn cuối). Mỗi mã hàng đúng một công đoạn cuối đang dùng (BE kiểm). */
export function OperationFormModal({
  open,
  productId,
  operation,
  groups,
  onClose,
  onCreated,
}: Props) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveOperation(operation?.operationId ?? null);
  const spec = useProductSpec(open ? productId : undefined);

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
            usesSemiFinished: operation.usesSemiFinished,
            groupName: operation.groupName ?? undefined,
            componentIds: operation.componentIds,
          }
        : { isFinalStep: false, usesSemiFinished: false, componentIds: [] },
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
        usesSemiFinished: values.usesSemiFinished,
        groupName: values.groupName?.trim() || null,
        componentIds: values.componentIds,
      },
      {
        onSuccess: (operationId) => {
          onClose();
          if (!operation) onCreated?.(operationId);
        },
      },
    );
  };

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.sm}
      title={operation ? text.edit : text.addTitle}
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
        <Form.Item
          name="stationSequence"
          label={text.form.sequence}
          rules={[{ required: true, message: PRODUCTION_TEXT.required }]}
        >
          <InputNumber min={1} precision={0} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="skillLevel" label={text.form.skill}>
          <InputNumber min={1} max={9} precision={0} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="standardTimeSec" label={text.form.standardTime}>
          <InputNumber min={0.01} precision={2} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="groupName" label={text.form.group} extra={text.form.groupHint}>
          <AutoComplete allowClear options={groups.map((g) => ({ value: g }))} maxLength={200} />
        </Form.Item>
        <Form.Item
          name="componentIds"
          label={text.form.components}
          extra={text.form.componentsHint}
        >
          <Select
            mode="multiple"
            allowClear
            optionFilterProp="label"
            loading={spec.isLoading}
            options={(spec.data?.components ?? []).map((c) => ({
              value: c.productComponentId,
              label: c.componentName,
            }))}
          />
        </Form.Item>
        <Form.Item name="usesSemiFinished" valuePropName="checked">
          <Switch checkedChildren={text.withPieces} unCheckedChildren={text.withoutPieces} />
        </Form.Item>
        <div style={{ marginTop: -16, marginBottom: 16, color: '#8c8c8c' }}>
          {text.form.usesSemiFinished}
        </div>
        <Form.Item name="isFinalStep" valuePropName="checked">
          <Checkbox>{text.form.isFinal}</Checkbox>
        </Form.Item>
      </Form>
    </Modal>
  );
}
