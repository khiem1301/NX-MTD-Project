import { Button, DatePicker, Drawer, Form, Input, InputNumber, Select, Space } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import { MESSAGES } from '@/constants/messages';
import { MANAGER_HIDDEN_ROLES, PAY_TYPE_LABEL, ROLE_LABEL, suggestPayType, type PayType } from '@/constants/hr';
import { ROLES, type Role } from '@/constants/roles';
import { useTeamLookup } from '@/hooks/useHrLookups';
import type { ApiError } from '@/types/api';
import { toApiDate } from '@/utils/format';
import { useCreateEmployee, useUpdateEmployee } from '../hooks/useEmployees';
import type { EmployeeDetail, TempPassword } from '../types';

type FormValues = {
  employeeCode: string;
  fullName: string;
  gender?: string;
  dateOfBirth?: Dayjs;
  phone?: string;
  role: Role;
  teamId?: number;
  payType: PayType;
  startDate: Dayjs;
  attendanceCode?: number;
};

type EmployeeFormDrawerProps = {
  open: boolean;
  employee: EmployeeDetail | null;
  /** Admin sửa đủ trường; Quản đốc sửa được hồ sơ, tổ và vai trò (trừ Admin / Quản đốc / Kế toán) nhưng không đổi kiểu lương (UC16). */
  isAdmin: boolean;
  onClose: () => void;
  onCreated: (password: TempPassword) => void;
};

const ROLE_OPTIONS = (Object.values(ROLES) as Role[]).map((r) => ({ value: r, label: ROLE_LABEL[r] }));
const PAY_OPTIONS = (Object.keys(PAY_TYPE_LABEL) as PayType[]).map((p) => ({ value: p, label: PAY_TYPE_LABEL[p] }));

/** UC16 / UC17: thêm (luôn kèm tài khoản) và sửa nhân viên. Bậc thợ chỉ đọc (KH-14). */
export function EmployeeFormDrawer({ open, employee, isAdmin, onClose, onCreated }: EmployeeFormDrawerProps) {
  const [form] = Form.useForm<FormValues>();
  const teams = useTeamLookup();
  const create = useCreateEmployee();
  const update = useUpdateEmployee(employee?.employeeId ?? 0);
  const saving = create.isPending || update.isPending;
  // Quản đốc không gán được vai trò mà mình không nhìn thấy (GBR-01).
  const roleOptions = isAdmin || employee === null ? ROLE_OPTIONS : ROLE_OPTIONS.filter((o) => !MANAGER_HIDDEN_ROLES.includes(o.value));

  const suggest = () => {
    const values = form.getFieldsValue();
    const teamType = teams.data?.find((t) => t.teamId === values.teamId)?.teamType;
    const pay = suggestPayType(values.role, teamType);
    if (pay && !employee) form.setFieldValue('payType', pay);
  };

  const showFieldErrors = (error: ApiError) =>
    form.setFields(Object.entries(error.errors ?? {}).map(([name, errors]) => ({ name: name as keyof FormValues, errors })));

  const handleFinish = (v: FormValues) => {
    const base = {
      fullName: v.fullName,
      gender: v.gender ?? null,
      dateOfBirth: v.dateOfBirth ? toApiDate(v.dateOfBirth) : null,
      phone: v.phone ?? null,
      startDate: toApiDate(v.startDate),
      attendanceCode: v.attendanceCode ?? null,
    };
    if (!employee) {
      create.mutate({ ...base, employeeCode: v.employeeCode, role: v.role, teamId: v.teamId ?? null, payType: v.payType }, {
        onSuccess: (data) => {
          onCreated({ username: data.username, temporaryPassword: data.temporaryPassword });
          onClose();
        },
        onError: showFieldErrors,
      });
      return;
    }
    const body = { ...base, role: v.role, teamId: v.teamId ?? null, payType: v.payType, rowVer: employee.rowVer };
    update.mutate(body, { onSuccess: onClose, onError: showFieldErrors });
  };

  const initialValues: Partial<FormValues> = employee
    ? {
        employeeCode: employee.employeeCode, fullName: employee.fullName, gender: employee.gender ?? undefined,
        dateOfBirth: employee.dateOfBirth ? dayjs(employee.dateOfBirth) : undefined, phone: employee.phone ?? undefined,
        role: employee.role, teamId: employee.teamId ?? undefined, payType: employee.payType,
        startDate: dayjs(employee.startDate), attendanceCode: employee.attendanceCode ?? undefined,
      }
    : { startDate: dayjs(), payType: 'Monthly' };

  return (
    <Drawer
      open={open}
      title={employee ? `${MESSAGES.hr.editEmployee} ${employee.employeeCode}` : MESSAGES.hr.addEmployee}
      width={520}
      onClose={onClose}
      destroyOnHidden
      extra={
        <Space>
          <Button onClick={onClose}>{MESSAGES.common.cancel}</Button>
          <Button type="primary" loading={saving} onClick={() => form.submit()}>{MESSAGES.common.save}</Button>
        </Space>
      }
    >
      <Form form={form} layout="vertical" preserve={false} initialValues={initialValues} onFinish={handleFinish} disabled={saving}>
        <Form.Item name="employeeCode" label={MESSAGES.hr.employeeCode} rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 20 }]}>
          <Input disabled={employee !== null} />
        </Form.Item>
        <Form.Item name="fullName" label={MESSAGES.hr.fullName} rules={[{ required: true, whitespace: true, message: MESSAGES.common.required }, { max: 100 }]}>
          <Input />
        </Form.Item>
        <Space wrap>
          <Form.Item name="gender" label={MESSAGES.hr.gender}>
            <Select allowClear style={{ width: 120 }} options={[{ value: 'M', label: MESSAGES.hr.male }, { value: 'F', label: MESSAGES.hr.female }]} />
          </Form.Item>
          <Form.Item name="dateOfBirth" label={MESSAGES.hr.dateOfBirth}>
            <DatePicker format="DD/MM/YYYY" disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item name="phone" label={MESSAGES.hr.phone} rules={[{ max: 15 }, { pattern: /^[0-9+]*$/, message: 'Chỉ gồm chữ số và dấu +' }]}>
            <Input style={{ width: 160 }} />
          </Form.Item>
        </Space>
        <Form.Item name="role" label={MESSAGES.hr.role} rules={[{ required: true, message: MESSAGES.common.required }]}>
          <Select options={roleOptions} onChange={suggest} />
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
        <Form.Item name="payType" label={MESSAGES.hr.payType} rules={[{ required: true, message: MESSAGES.common.required }]}>
          <Select options={PAY_OPTIONS} />
        </Form.Item>
        {employee && (
          <Form.Item label={MESSAGES.hr.grade} extra={MESSAGES.hr.gradeHint}>
            <Input disabled value={employee.currentGradeCode ?? '—'} />
          </Form.Item>
        )}
        <Space wrap>
          <Form.Item name="attendanceCode" label={MESSAGES.hr.attendanceCode}>
            <InputNumber min={1} max={99999999} precision={0} controls={false} />
          </Form.Item>
          <Form.Item name="startDate" label={MESSAGES.hr.startDate} rules={[{ required: true, message: MESSAGES.common.required }]}>
            <DatePicker format="DD/MM/YYYY" />
          </Form.Item>
        </Space>
      </Form>
    </Drawer>
  );
}
