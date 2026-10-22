import { useEffect } from 'react';
import { DatePicker, Form, Input, Modal, Select, TimePicker } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { REQUEST_TYPE } from '@/constants/requestTypes';
import { useRequestInbox } from '@/features/requests/hooks/useRequests';
import { toApiDate } from '@/utils/format';
import { ATTENDANCE_TEXT } from '../constants';
import { useCreateAdjustment } from '../hooks/useAttendance';
import type { AdjustmentType } from '../types';
import { EmployeePicker } from './EmployeePicker';

/** Điền sẵn khi Kế toán bấm "Chấm hộ" trên một đơn xin chấm bù đã duyệt. */
export type AdjustmentPrefill = { requestId: number; workDate: string | null; reason: string | null };

type Props = { open: boolean; prefill: AdjustmentPrefill | null; onClose: () => void };

type FormValues = {
  employeeId: number;
  workDate: Dayjs;
  adjustmentType: AdjustmentType;
  requestId?: number;
  reason: string;
  checkIn?: Dayjs;
  checkOut?: Dayjs;
};

const text = ATTENDANCE_TEXT.adjust;
const required = [{ required: true, message: text.required }];
const TYPE_OPTIONS = (Object.keys(text.types) as AdjustmentType[]).map((value) => ({ value, label: text.types[value] }));
const TIME_FORMAT = 'HH:mm';

/**
 * GBR-25 — Kế toán chấm công hộ: bắt buộc lý do và ít nhất giờ vào hoặc giờ ra. Loại "Theo đơn xin chấm bù" phải chọn một
 * đơn đã được duyệt. BE chặn chấm trùng ngày và tháng đã khóa.
 */
export function AdjustmentFormModal({ open, prefill, onClose }: Props) {
  const [form] = Form.useForm<FormValues>();
  const create = useCreateAdjustment();
  const type = Form.useWatch('adjustmentType', form);
  const requests = useRequestInbox({ page: 1, pageSize: 50, requestType: REQUEST_TYPE.AttendanceCorrection, status: 'Approved' });

  useEffect(() => {
    if (!open) return;
    form.resetFields();
    form.setFieldsValue({
      workDate: prefill?.workDate ? dayjs(prefill.workDate) : dayjs(),
      adjustmentType: prefill ? 'APPROVED_REQUEST' : 'FORGOT_PUNCH',
      requestId: prefill?.requestId,
      reason: prefill?.reason ?? undefined,
      checkIn: dayjs('07:30', TIME_FORMAT),
      checkOut: dayjs('16:30', TIME_FORMAT),
    });
  }, [open, prefill, form]);

  const handleFinish = (values: FormValues) => {
    create.mutate(
      {
        employeeId: values.employeeId,
        workDate: toApiDate(values.workDate),
        adjustmentType: values.adjustmentType,
        reason: values.reason.trim(),
        requestId: values.adjustmentType === 'APPROVED_REQUEST' ? values.requestId : undefined,
        checkIn: values.checkIn ? `${values.checkIn.format(TIME_FORMAT)}:00` : undefined,
        checkOut: values.checkOut ? `${values.checkOut.format(TIME_FORMAT)}:00` : undefined,
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Modal
      open={open}
      width={560}
      title={text.addTitle}
      okText={MESSAGES.common.save}
      cancelText={MESSAGES.common.cancel}
      confirmLoading={create.isPending}
      onOk={() => form.submit()}
      onCancel={onClose}
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={handleFinish} disabled={create.isPending}>
        <Form.Item name="employeeId" label={text.employee} rules={required}>
          <EmployeePicker />
        </Form.Item>
        <Form.Item name="workDate" label={text.workDate} rules={required}>
          <DatePicker format="DD/MM/YYYY" allowClear={false} />
        </Form.Item>
        <Form.Item name="adjustmentType" label={text.type} rules={required}>
          <Select options={TYPE_OPTIONS} />
        </Form.Item>
        {type === 'APPROVED_REQUEST' && (
          <Form.Item name="requestId" label={text.request} rules={[{ required: true, message: text.requestRequired }]}>
            <Select
              showSearch
              optionFilterProp="label"
              loading={requests.isLoading}
              options={(requests.data?.items ?? []).map((r) => ({
                value: r.requestId,
                label: `${r.requestCode} - ${r.title ?? ''}${r.targetDate ? ` (${r.targetDate})` : ''}`,
              }))}
            />
          </Form.Item>
        )}
        <Form.Item
          label={`${text.checkIn} / ${text.checkOut}`}
          required
          extra={text.needTime}
          style={{ marginBottom: 12 }}
        >
          <Form.Item name="checkIn" noStyle>
            <TimePicker format={TIME_FORMAT} placeholder={text.checkIn} style={{ marginRight: 12 }} />
          </Form.Item>
          <Form.Item name="checkOut" noStyle>
            <TimePicker format={TIME_FORMAT} placeholder={text.checkOut} />
          </Form.Item>
        </Form.Item>
        <Form.Item name="reason" label={text.reason} rules={[{ required: true, whitespace: true, message: text.required }]}>
          <Input.TextArea rows={3} maxLength={500} showCount />
        </Form.Item>
      </Form>
    </Modal>
  );
}
