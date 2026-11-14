import { useEffect } from 'react';
import { Alert, Form, Input, Modal, Select } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useTeamMemberLookup } from '@/hooks/useHrLookups';
import { MODAL_WIDTH } from '@/components/common/modalSize';
import { QtyInput } from '@/components/common/QtyInput';
import {
  DEFECT_MAX_QTY,
  DEFECT_REASON_OPTIONS,
  DEFECT_TEXT_MAX,
  PRODUCTION_TEXT,
  SHIFT_OPTIONS,
  toShiftCode,
} from '../constants';
import { useAssignableOperations } from '../hooks/useAssignments';
import { useUpdateDefect } from '../hooks/useDefects';
import type { Defect, DefectReason, DefectType, OutputRole, ShiftCode } from '../types';

type DefectEditModalProps = {
  role: OutputRole;
  defect: Defect | null;
  onClose: () => void;
};

type FormValues = {
  shiftCode?: ShiftCode;
  operationId: number;
  responsibleWorkerId: number;
  defectType: DefectType;
  quantity: number;
  reasonCode: DefectReason;
  reasonNote?: string;
  reason?: string;
};

const text = PRODUCTION_TEXT.defects;
const required = [{ required: true, message: PRODUCTION_TEXT.required }];

/**
 * Sửa phiếu lỗi: không đổi được ngày và dòng đơn (ghi sai thì hủy rồi ghi lại). Tổ trưởng sửa trong 24 giờ kể từ lúc lập;
 * Quản đốc sửa được sau đó nhưng bắt buộc nhập lý do. Các ràng buộc khác (kỳ lương khóa, đơn đóng, đã có đơn cắt bù) do BE báo.
 */
export function DefectEditModal({ role, defect, onClose }: DefectEditModalProps) {
  const [form] = Form.useForm<FormValues>();
  const isManager = role === 'manager';
  const update = useUpdateDefect(role);
  const defectType = Form.useWatch('defectType', form);
  const reasonCode = Form.useWatch('reasonCode', form);
  const isOutputError = defectType === 'OutputErr';
  const operations = useAssignableOperations(defect?.orderId ?? null);
  const members = useTeamMemberLookup(defect?.sewingTeamId ?? null);

  useEffect(() => {
    if (!defect) return;
    form.resetFields();
    form.setFieldsValue({
      shiftCode: defect.shiftCode ? toShiftCode(defect.shiftCode) : undefined,
      operationId: defect.operationId,
      responsibleWorkerId: defect.responsibleWorkerId,
      defectType: defect.defectType,
      quantity: defect.quantity,
      reasonCode: defect.defectReasonCode,
      reasonNote: defect.reasonNote ?? undefined,
    });
  }, [defect, form]);

  const handleFinish = (values: FormValues) => {
    if (!defect) return;
    update.mutate(
      {
        id: defect.defectRecordId,
        body: {
          shiftCode: values.shiftCode,
          operationId: values.operationId,
          responsibleWorkerId: values.responsibleWorkerId,
          defectType: values.defectType,
          quantity: values.quantity,
          reasonCode: isOutputError ? 'Other' : values.reasonCode,
          reasonNote: values.reasonNote?.trim() || undefined,
          reason: values.reason?.trim() || undefined,
          rowVer: defect.rowVer,
        },
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={defect !== null}
      width={MODAL_WIDTH.sm}
      title={`${text.editTitle}${defect ? ` · ${defect.defectCode}` : ''}`}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={update.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Alert type="info" showIcon message={text.editHint} style={{ marginBottom: 16 }} />
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={update.isPending}>
        <Form.Item name="shiftCode" label={text.form.shift}>
          <Select allowClear options={SHIFT_OPTIONS} style={{ width: 160 }} />
        </Form.Item>
        <Form.Item name="operationId" label={text.form.operation} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            loading={operations.isLoading}
            options={(operations.data ?? []).map((o) => ({
              value: o.operationId,
              label: `${o.productCode} · ${o.operationCode} - ${o.operationName}`,
            }))}
          />
        </Form.Item>
        <Form.Item name="responsibleWorkerId" label={text.form.worker} rules={required}>
          <Select
            showSearch
            optionFilterProp="label"
            loading={members.isLoading}
            options={(members.data ?? [])
              .filter((m) => m.role === 'Worker')
              .map((m) => ({ value: m.employeeId, label: `${m.employeeCode} - ${m.fullName}` }))}
          />
        </Form.Item>
        <Form.Item name="defectType" label={text.form.type} rules={required}>
          <Select
            style={{ width: 220 }}
            options={(Object.keys(text.type) as DefectType[]).map((value) => ({
              value,
              label: text.type[value].text,
            }))}
          />
        </Form.Item>
        <Form.Item
          name="quantity"
          label={text.form.qty}
          rules={[
            ...required,
            { type: 'number', min: 1, message: text.form.qtyMin },
            { type: 'number', max: DEFECT_MAX_QTY, message: text.form.qtyMax },
          ]}
        >
          <QtyInput min={1} max={DEFECT_MAX_QTY} style={{ width: 160 }} />
        </Form.Item>
        {!isOutputError && (
          <Form.Item name="reasonCode" label={text.form.reason} rules={required}>
            <Select options={DEFECT_REASON_OPTIONS} style={{ width: 260 }} />
          </Form.Item>
        )}
        <Form.Item
          name="reasonNote"
          label={isOutputError ? text.form.outputErrorNote : text.form.note}
          rules={[
            {
              required: isOutputError || reasonCode === 'Other',
              whitespace: true,
              message: isOutputError ? text.form.outputErrorNoteRequired : text.form.noteRequired,
            },
          ]}
        >
          <Input.TextArea rows={2} maxLength={DEFECT_TEXT_MAX} showCount />
        </Form.Item>
        <Form.Item
          name="reason"
          label={text.editReason}
          rules={[{ required: isManager, whitespace: true, message: text.editReasonRequired }]}
        >
          <Input.TextArea rows={2} maxLength={DEFECT_TEXT_MAX} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
