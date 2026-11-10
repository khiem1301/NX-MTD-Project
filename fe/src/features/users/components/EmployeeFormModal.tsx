import { useState } from 'react';
import { Button, Form, Input, InputNumber, Select, Space } from 'antd';
import dayjs from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { ATTENDANCE_TEXT } from '@/features/attendance/constants';
import { RjEmployeePickerModal } from '@/features/attendance/components/RjEmployeePickerModal';
import { normalizePhone, phoneRule } from '@/utils/phone';
import {
  CODE_PATTERN,
  normalizeCode,
  normalizePersonName,
  PERSON_NAME_PATTERN,
} from '@/utils/textInput';
import { PAY_TYPE_LABEL, type PayType } from '@/constants/hr';
import type { EmployeeDetail, TempPassword } from '../types';
import { DetailModal } from '@/components/common/DetailModal';
import { AppDatePicker } from '@/components/common/DateInputs';
import { useEmployeeForm } from '../hooks/useEmployeeForm';

export type EmployeeDraft = {
  fullName?: string;
  attendanceCode?: number;
};

export type EmployeeFormModalProps = {
  open: boolean;
  employee: EmployeeDetail | null;
  /** Prefill khi tạo mới (vd chọn từ RJ). */
  draft?: EmployeeDraft | null;
  /** Admin sửa đủ trường; Quản đốc sửa được hồ sơ, tổ và vai trò (trừ Admin / Quản đốc / Kế toán) nhưng không đổi kiểu lương (UC16). */
  isAdmin: boolean;
  onClose: () => void;
  onCreated: (password: TempPassword) => void;
};

const PAY_OPTIONS = (Object.keys(PAY_TYPE_LABEL) as PayType[]).map((p) => ({
  value: p,
  label: PAY_TYPE_LABEL[p],
}));

/** UC16 / UC17: thêm (luôn kèm tài khoản) và sửa nhân viên. Bậc thợ chỉ đọc (KH-14). */
export function EmployeeFormModal({
  open,
  employee,
  draft,
  isAdmin,
  onClose,
  onCreated,
}: EmployeeFormModalProps) {
  const [rjOpen, setRjOpen] = useState(false);
  const { teams, saving, roleOptions, roleLocked, suggest, handleFinish, initialValues, form } =
    useEmployeeForm({ employee, isAdmin, onClose, onCreated, draft });

  return (
    <DetailModal
      open={open}
      title={
        employee ? `${MESSAGES.hr.editEmployee} ${employee.employeeCode}` : MESSAGES.hr.addEmployee
      }
      onClose={onClose}
      extra={
        <Space>
          <Button onClick={onClose}>{MESSAGES.common.cancel}</Button>
          <Button type="primary" loading={saving} onClick={() => form.submit()}>
            {MESSAGES.common.save}
          </Button>
        </Space>
      }
      size="sm"
    >
      <Form
        form={form}
        layout="vertical"
        preserve={false}
        initialValues={initialValues}
        onFinish={handleFinish}
        disabled={saving}
      >
        <Form.Item
          name="employeeCode"
          label={MESSAGES.hr.employeeCode}
          normalize={normalizeCode}
          rules={[
            { required: true, whitespace: true, message: MESSAGES.common.required },
            { max: 20 },
            { pattern: CODE_PATTERN, message: MESSAGES.hr.codeInvalid },
          ]}
        >
          <Input disabled={employee !== null} />
        </Form.Item>
        <Form.Item
          name="fullName"
          label={MESSAGES.hr.fullName}
          normalize={normalizePersonName}
          rules={[
            { required: true, whitespace: true, message: MESSAGES.common.required },
            { max: 100 },
            { pattern: PERSON_NAME_PATTERN, message: MESSAGES.hr.fullNameInvalid },
          ]}
        >
          <Input />
        </Form.Item>
        <Space wrap>
          <Form.Item name="gender" label={MESSAGES.hr.gender}>
            <Select
              allowClear
              style={{ width: 120 }}
              options={[
                { value: 'M', label: MESSAGES.hr.male },
                { value: 'F', label: MESSAGES.hr.female },
              ]}
            />
          </Form.Item>
          <Form.Item name="dateOfBirth" label={MESSAGES.hr.dateOfBirth}>
            <AppDatePicker disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item
            name="phone"
            normalize={normalizePhone}
            label={MESSAGES.hr.phone}
            rules={[phoneRule]}
          >
            <Input maxLength={12} style={{ width: 160 }} />
          </Form.Item>
        </Space>
        <Form.Item
          name="role"
          label={MESSAGES.hr.role}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <Select options={roleOptions} onChange={suggest} disabled={roleLocked} />
        </Form.Item>
        <Form.Item name="teamId" label={MESSAGES.hr.team}>
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            options={(teams.data ?? []).map((t) => ({ value: t.teamId, label: t.teamName }))}
            onChange={suggest}
          />
        </Form.Item>
        <Form.Item
          name="payType"
          label={MESSAGES.hr.payType}
          rules={[{ required: true, message: MESSAGES.common.required }]}
        >
          <Select options={PAY_OPTIONS} />
        </Form.Item>
        {employee && (
          <Form.Item label={MESSAGES.hr.grade} extra={MESSAGES.hr.gradeHint}>
            <Input disabled value={employee.currentGradeCode ?? '—'} />
          </Form.Item>
        )}
        <Space wrap align="start">
          <Form.Item name="attendanceCode" label={MESSAGES.hr.attendanceCode}>
            <InputNumber min={1} max={99999999} precision={0} controls={false} />
          </Form.Item>
          {!employee ? (
            <Form.Item label=" ">
              <Button onClick={() => setRjOpen(true)}>{ATTENDANCE_TEXT.rj.pick}</Button>
            </Form.Item>
          ) : null}
          <Form.Item
            name="startDate"
            label={MESSAGES.hr.startDate}
            rules={[{ required: true, message: MESSAGES.common.required }]}
          >
            <AppDatePicker />
          </Form.Item>
        </Space>
      </Form>
      <RjEmployeePickerModal
        open={rjOpen}
        onClose={() => setRjOpen(false)}
        onPick={(row) => {
          form.setFieldsValue({
            fullName: row.fullName,
            attendanceCode: row.attendanceCode,
          });
        }}
      />
    </DetailModal>
  );
}
