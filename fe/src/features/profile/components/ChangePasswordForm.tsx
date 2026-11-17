import { Button, Form, message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useChangePassword } from '../hooks/useProfile';
import type { ChangePasswordPayload } from '../api/profileApi';
import { PasswordFields } from './PasswordFields';
import { showPasswordError } from './passwordErrors';

/** UC03 (màn bắt buộc đổi mật khẩu tạm): nhập lại phải khớp (MSG05); sai mật khẩu hiện tại là 409 (MSG07) nên không bị tự refresh token. */
export function ChangePasswordForm({ requireCurrent = true }: { requireCurrent?: boolean }) {
  const [form] = Form.useForm<ChangePasswordPayload>();
  const change = useChangePassword();

  const handleFinish = (values: ChangePasswordPayload) =>
    change.mutate(values, {
      onSuccess: () => {
        void message.success(MESSAGES.hr.passwordChanged);
        form.resetFields();
      },
      onError: (error) => {
        if (!showPasswordError(form, error)) void message.error(error.message);
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
      <PasswordFields requireCurrent={requireCurrent} />
      <Button type="primary" htmlType="submit" loading={change.isPending}>
        {MESSAGES.hr.changePasswordTitle}
      </Button>
    </Form>
  );
}
