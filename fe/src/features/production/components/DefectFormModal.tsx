import { useEffect } from 'react';
import { Alert, Form, Input, Modal, Select } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { useWorkDateRange } from '@/hooks/useLookups';
import { OrderSelect } from '@/components/common/OrderSelect';
import { MESSAGES } from '@/constants/messages';
import { useAuth } from '@/hooks/useAuth';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { useOrderLineLookup } from '@/hooks/useOrderLookups';
import { toApiDate } from '@/utils/format';
import {
  DEFECT_MAX_QTY,
  DEFECT_REASON_OPTIONS,
  DEFECT_TEXT_MAX,
  PRODUCTION_TEXT,
  SHIFT_OPTIONS,
} from '../constants';
import { useAssignableOperations } from '../hooks/useAssignments';
import { useCreateDefect } from '../hooks/useDefects';
import type { DefectReason, DefectType, ShiftCode } from '../types';
import { AppDatePicker } from '@/components/common/DateInputs';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { QtyInput } from '@/components/common/QtyInput';

/** onCreated: báo phiếu vừa ghi (id + loại) để trang mở luôn form đơn cắt bù khi là hàng hủy (Scrap). */
type Props = {
  open: boolean;
  onClose: () => void;
  onCreated?: (defectId: number, defectType: DefectType) => void;
};

type FormValues = {
  workDate: Dayjs;
  shiftCode?: ShiftCode;
  orderId: number;
  orderLineId: number;
  operationId: number;
  responsibleWorkerId: number;
  defectType: DefectType;
  quantity: number;
  reasonCode: DefectReason;
  reasonNote?: string;
};

const text = PRODUCTION_TEXT.defects.form;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

/**
 * UC56 — tổ trưởng / tổ phó may ghi phiếu lỗi: sửa lại (Rework), hủy (Scrap, khóa đơn giá để trừ lương tạm tính) hoặc nhập sai sản lượng
 * (OutputErr, trừ khỏi sản lượng và tiền của công nhân; chỉ cần ghi chú lý do, DEV-ALL-79).
 */
export function DefectFormModal({ open, onClose, onCreated }: Props) {
  const [form] = Form.useForm<FormValues>();
  const { user } = useAuth();
  const create = useCreateDefect();
  const orderId = Form.useWatch('orderId', form) ?? null;
  const disabledWorkDate = useWorkDateRange(orderId ?? undefined);
  const reasonCode = Form.useWatch('reasonCode', form);
  const defectType = Form.useWatch('defectType', form);
  const isOutputError = defectType === 'OutputErr';
  const lines = useOrderLineLookup(orderId);
  const operations = useAssignableOperations(orderId);
  const members = useTeamMemberLookup(user?.teamId ?? null);

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({ workDate: dayjs(), defectType: 'Rework', quantity: 1 });
  }, [open, form]);

  const handleFinish = (values: FormValues) => {
    create.mutate(
      {
        workDate: toApiDate(values.workDate),
        shiftCode: values.shiftCode,
        orderLineId: values.orderLineId,
        operationId: values.operationId,
        responsibleWorkerId: values.responsibleWorkerId,
        defectType: values.defectType,
        quantity: values.quantity,
        reasonCode: isOutputError ? 'Other' : values.reasonCode,
        reasonNote: values.reasonNote?.trim() || undefined,
      },
      {
        onSuccess: (defectId) => {
          onClose();
          onCreated?.(defectId, values.defectType);
        },
      },
    );
  };

  return (
    <Modal
      open={open}
      width={MODAL_WIDTH.sm}
      title={PRODUCTION_TEXT.defects.addTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={create.isPending}>
        <Form.Item name="workDate" label={text.date} rules={required}>
          <AppDatePicker allowClear={false} disabledDate={disabledWorkDate} />
        </Form.Item>
        <Form.Item name="shiftCode" label={text.shift}>
          <Select allowClear options={SHIFT_OPTIONS} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="orderId" label={PRODUCTION_TEXT.selectOrder} rules={required}>
          <OrderSelect
            width={460}
            onChange={() => form.setFieldsValue({ orderLineId: undefined, operationId: undefined })}
          />
        </Form.Item>
        <Form.Item name="orderLineId" label={text.line} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            disabled={orderId === null}
            loading={lines.isLoading}
            options={(lines.data ?? []).map((l) => ({
              value: l.orderLineId,
              label: `${l.productCode} - ${l.color} - ${l.sizeCode}`,
            }))}
          />
        </Form.Item>
        <Form.Item name="operationId" label={text.operation} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            disabled={orderId === null}
            loading={operations.isLoading}
            options={(operations.data ?? []).map((o) => ({
              value: o.operationId,
              label: `${o.productCode} · ${o.operationCode} - ${o.operationName}`,
            }))}
          />
        </Form.Item>
        <Form.Item name="responsibleWorkerId" label={text.worker} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            loading={members.isLoading}
            options={(members.data ?? [])
              .filter((m) => m.role === 'Worker')
              .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` }))}
          />
        </Form.Item>
        <Form.Item name="defectType" label={text.type} rules={required}>
          <Select
            style={{ width: 220 }}
            options={(Object.keys(PRODUCTION_TEXT.defects.type) as DefectType[]).map((value) => ({
              value,
              label: PRODUCTION_TEXT.defects.type[value].text,
            }))}
          />
        </Form.Item>
        <Form.Item
          name="quantity"
          label={text.qty}
          rules={[
            ...required,
            { type: 'number', min: 1, message: text.qtyMin },
            { type: 'number', max: DEFECT_MAX_QTY, message: text.qtyMax },
          ]}
        >
          <QtyInput min={1} max={DEFECT_MAX_QTY} style={{ width: 160 }} />
        </Form.Item>
        {isOutputError ? (
          <Alert type="info" showIcon message={text.outputErrorHint} style={{ marginBottom: 16 }} />
        ) : (
          <Form.Item name="reasonCode" label={text.reason} rules={required}>
            <Select options={DEFECT_REASON_OPTIONS} style={{ width: 260 }} />
          </Form.Item>
        )}
        <Form.Item
          name="reasonNote"
          label={isOutputError ? text.outputErrorNote : text.note}
          rules={[
            {
              required: isOutputError || reasonCode === 'Other',
              whitespace: true,
              message: isOutputError ? text.outputErrorNoteRequired : text.noteRequired,
            },
          ]}
        >
          <Input.TextArea rows={2} maxLength={DEFECT_TEXT_MAX} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
