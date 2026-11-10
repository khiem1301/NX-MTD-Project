import { useEffect } from 'react';
import { AutoComplete, Col, Form, Input, InputNumber, Modal, Row, Select } from 'antd';
import { normalizeCode } from '@/utils/textInput';
import { MESSAGES } from '@/constants/messages';
import { useProductComponents } from '@/hooks/useOrderLookups';
import { OPERATION_LIMITS, PRODUCTION_TEXT } from '../constants';
import { useSaveOperation } from '../hooks/useOperations';
import type { Operation } from '../types';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { OperationFinalField } from './OperationFinalField';

type Props = {
  open: boolean;
  productId: number;
  operation: Operation | null;
  /** Nhóm đã có của mã hàng (gợi ý khi gõ). */
  groups: string[];
  /** Mã hàng có công đoạn cuối đang dùng khác công đoạn này (tick ở đây sẽ chuyển cờ sang đây). */
  hasOtherFinal: boolean;
  /** Thứ tự và mã gợi ý khi thêm mới. */
  defaults: { stationSequence: number; operationCode: string };
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
  groupName?: string;
  componentIds: number[];
};

const text = PRODUCTION_TEXT.operations;
/** DEV-ALL-86: công đoạn chỉ gắn chi tiết cắt (đúng giá trị API định mức trả về); mỗi chi tiết cắt một vải nên gắn chi tiết là biết vải. */
const COMPONENT_TYPE_PANEL = 'Panel';
const required = [{ required: true, whitespace: true, message: PRODUCTION_TEXT.required }];

/** UC25 — Kế toán, Admin sửa một công đoạn đã nhập từ Excel (vd. đánh dấu công đoạn cuối). Mỗi mã hàng đúng một công đoạn cuối đang dùng (BE kiểm). */
export function OperationFormModal({
  open,
  productId,
  operation,
  groups,
  hasOtherFinal,
  defaults,
  onClose,
  onCreated,
}: Props) {
  const [form] = Form.useForm<FormValues>();
  const save = useSaveOperation(operation?.operationId ?? null);
  const components = useProductComponents(open ? productId : undefined);

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
            groupName: operation.groupName ?? undefined,
            componentIds: operation.componentIds,
          }
        : { ...defaults, isFinalStep: false, componentIds: [] },
    );
    // Chỉ điền lúc mở hộp thoại; danh sách tải lại sau đó không được ghi đè chỗ đang nhập.
    // eslint-disable-next-line react-hooks/exhaustive-deps
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
        teamType: operation?.teamType ?? 'Sewing',
        isFinalStep: values.isFinalStep,
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
        <Row gutter={12}>
          <Col xs={24} sm={8}>
            <Form.Item
              name="operationCode"
              label={text.form.code}
              normalize={normalizeCode}
              rules={[
                ...required,
                { pattern: OPERATION_LIMITS.codePattern, message: text.form.codeFormat },
              ]}
            >
              <Input maxLength={OPERATION_LIMITS.maxCode} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={16}>
            <Form.Item name="operationName" label={text.form.name} rules={required}>
              <Input maxLength={OPERATION_LIMITS.maxName} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={8}>
            <Form.Item
              name="stationSequence"
              label={text.form.sequence}
              rules={[{ required: true, message: PRODUCTION_TEXT.required }]}
            >
              <InputNumber
                min={1}
                max={OPERATION_LIMITS.maxSequence}
                precision={0}
                style={{ width: '100%' }}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={8}>
            <Form.Item name="skillLevel" label={text.form.skill}>
              <InputNumber
                min={OPERATION_LIMITS.minSkill}
                max={OPERATION_LIMITS.maxSkill}
                precision={0}
                style={{ width: '100%' }}
              />
            </Form.Item>
          </Col>
          <Col xs={24} sm={8}>
            <Form.Item name="standardTimeSec" label={text.form.standardTime}>
              <InputNumber
                min={0.01}
                max={OPERATION_LIMITS.maxStandardTimeSec}
                precision={2}
                style={{ width: '100%' }}
              />
            </Form.Item>
          </Col>
        </Row>
        <Form.Item name="groupName" label={text.form.group} extra={text.form.groupHint}>
          <AutoComplete
            allowClear
            options={groups.map((g) => ({ value: g }))}
            filterOption
            maxLength={OPERATION_LIMITS.maxGroupName}
          />
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
            loading={components.isLoading}
            options={(components.data ?? [])
              .filter((c) => c.componentType === COMPONENT_TYPE_PANEL)
              .map((c) => ({ value: c.productComponentId, label: c.componentName }))}
          />
        </Form.Item>
        <OperationFinalField hasOtherFinal={hasOtherFinal} />
      </Form>
    </Modal>
  );
}
