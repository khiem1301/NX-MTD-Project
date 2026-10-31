import { Button, Form, Input, message } from 'antd';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import { useChangePassword } from '../hooks/useProfile';
import type { ChangePasswordPayload } from '../api/profileApi';

/** UC03: nhập lại phải khớp (MSG05); sai mật khẩu hiện tại là 409 (MSG07) nên không bị tự refresh token. */
export function ChangePasswordForm() {
  const [form] = Form.useForm<ChangePasswordPayload>();
  const change = useChangePassword();

  const handleFinish = (values: ChangePasswordPayload) =>
    change.mutate(values, {
      onSuccess: () => {
        void message.success(MESSAGES.hr.passwordChanged);
        form.resetFields();
      },
      onError: (error) => {
        if (error.errorCode === ERROR_CODES.AuthWrongCurrentPassword) {
          form.setFields([{ name: 'currentPassword', errors: [MESSAGES.hr.wrongCurrentPassword] }]);
          return;
        }
        form.setFields(
          Object.entries(error.errors ?? {}).map(([name, errors]) => ({
            name: name as keyof ChangePasswordPayload,
            errors,
          })),
        );
        if (!error.errors) void message.error(error.message);
      },
    });

  return (
    <Form
      form={form}
      layout="vertical"
      onFinish={handleFinish}
      disabled={change.isPending}
      style={{ maxWidth: 420 }}
    >
      <Form.Item
        name="currentPassword"
        label={MESSAGES.hr.currentPassword}
        rules={[{ required: true, message: MESSAGES.common.required }]}
      >
        <Input.Password autoComplete="current-password" />
      </Form.Item>
      <Form.Item
        name="newPassword"
        label={MESSAGES.hr.newPassword}
        rules={[{ required: true, message: MESSAGES.common.required }, { max: 100 }]}
      >
        <Input.Password autoComplete="new-password" />
      </Form.Item>
      <Form.Item
        name="confirmPassword"
        label={MESSAGES.hr.confirmPassword}
        dependencies={['newPassword']}
        rules={[
          { required: true, message: MESSAGES.common.required },
          ({ getFieldValue }) => ({
            validator: (_, value: string | undefined) =>
              !value || value === getFieldValue('newPassword')
                ? Promise.resolve()
                : Promise.reject(new Error(MESSAGES.hr.passwordMismatch)),
          }),
        ]}
      >
        <Input.Password autoComplete="new-password" />
      </Form.Item>
      <Button type="primary" htmlType="submit" loading={change.isPending}>
        {MESSAGES.hr.changePasswordTitle}
      </Button>
    </Form>
  );
}
