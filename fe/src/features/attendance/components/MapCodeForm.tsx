import { Alert, Button, Descriptions, Form, InputNumber, Space } from 'antd';
import { ATTENDANCE_TEXT } from '../constants';
import type { UnmappedCode } from '../types';
import { formatDateTime } from '../utils';

type FormValues = { employeeId: number };

type Props = {
  code: UnmappedCode;
  submitting: boolean;
  onSubmit: (employeeId: number) => void;
  onCancel: () => void;
};

/**
 * TODO(Minh, MI-02): đổi InputNumber thành Select tìm theo tên / mã nhân viên khi Khiêm xong
 * API danh sách nhân viên (KH-06, hạn 24/10). Hiện nhập thẳng EmployeeId để không chặn luồng.
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
        rules={[
          { required: true, message: 'Vui lòng nhập mã nhân viên.' },
          { type: 'number', min: 1, message: 'Mã nhân viên phải là số dương.' },
        ]}
      >
        <InputNumber
          style={{ width: '100%' }}
          min={1}
          placeholder={ATTENDANCE_TEXT.map.employeePlaceholder}
          autoFocus
        />
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
