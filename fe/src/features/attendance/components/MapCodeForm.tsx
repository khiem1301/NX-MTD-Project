import { Alert, Button, Descriptions, Form, Space } from 'antd';
import { ATTENDANCE_TEXT } from '../constants';
import type { UnmappedCode } from '../types';
import { EmployeePicker } from './EmployeePicker';
import { formatDateTime } from '../utils';

type FormValues = { employeeId: number };

type Props = {
  code: UnmappedCode;
  submitting: boolean;
  onSubmit: (employeeId: number) => void;
  onCancel: () => void;
};

/**
 * Chọn nhân viên theo tổ rồi tên (API tra cứu nhân sự dùng chung) thay cho việc gõ tay mã nhân viên.
 */
export function MapCodeForm({ code, submitting, onSubmit, onCancel }: Props) {
  const [form] = Form.useForm<FormValues>();

  return (
    <Form
      form={form}
      layout="vertical"
      requiredMark={false}
      onFinish={(values) => onSubmit(values.employeeId)}
    >
      <Descriptions size="small" column={1} bordered style={{ marginBottom: 16 }}>
        <Descriptions.Item label={ATTENDANCE_TEXT.table.attendanceCode}>
          {code.attendanceCode}
        </Descriptions.Item>
        <Descriptions.Item label={ATTENDANCE_TEXT.table.punchCount}>
          {code.punchCount}
        </Descriptions.Item>
        <Descriptions.Item label={ATTENDANCE_TEXT.table.lastPunch}>
          {formatDateTime(code.lastPunchAt)}
        </Descriptions.Item>
      </Descriptions>

      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message={ATTENDANCE_TEXT.map.description.replace('{0}', String(code.attendanceCode))}
      />

      <Form.Item
        name="employeeId"
        label={ATTENDANCE_TEXT.map.employee}
        rules={[{ required: true, message: 'Vui lòng chọn nhân viên.' }]}
      >
        <EmployeePicker />
      </Form.Item>

      <Space style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <Button onClick={onCancel} disabled={submitting}>
          {ATTENDANCE_TEXT.map.cancel}
        </Button>
        <Button type="primary" htmlType="submit" loading={submitting}>
          {ATTENDANCE_TEXT.map.submit}
        </Button>
      </Space>
    </Form>
  );
}
