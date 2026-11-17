import { Form, Input } from 'antd';
import type { Rule } from 'antd/es/form';
import { PASSWORD_MAX_LENGTH } from '@/constants/limits';
import { MESSAGES } from '@/constants/messages';

const FIELD_NAMES = ['currentPassword', 'newPassword', 'confirmPassword'] as const;

type Props = {
  /** Bỏ ô mật khẩu hiện tại khi tài khoản đang dùng mật khẩu tạm. */
  requireCurrent?: boolean;
  /** Dùng khi đặt trong form khác: để trống cả ba ô là không đổi mật khẩu, nhập một ô thì phải nhập đủ. */
  optional?: boolean;
};

/** Ba ô của UC03: nhập lại phải khớp (MSG05). Dùng trong form đổi mật khẩu và hộp thoại sửa hồ sơ. */
export function PasswordFields({ requireCurrent = true, optional = false }: Props) {
  const requiredRule: Rule = optional
    ? ({ getFieldValue }) => ({
        validator: (_, value: string | undefined) =>
          value || !FIELD_NAMES.some((name) => getFieldValue(name))
            ? Promise.resolve()
            : Promise.reject(new Error(MESSAGES.common.required)),
      })
    : { required: true, message: MESSAGES.common.required };

  return (
    <>
      {requireCurrent && (
        <Form.Item
          name="currentPassword"
          label={MESSAGES.hr.currentPassword}
          rules={[requiredRule]}
        >
          <Input.Password autoComplete="current-password" />
        </Form.Item>
      )}
      <Form.Item
        name="newPassword"
        label={MESSAGES.hr.newPassword}
        rules={[requiredRule, { max: PASSWORD_MAX_LENGTH, message: MESSAGES.hr.passwordTooLong }]}
      >
        <Input.Password autoComplete="new-password" />
      </Form.Item>
      <Form.Item
        name="confirmPassword"
        label={MESSAGES.hr.confirmPassword}
        dependencies={['newPassword']}
        rules={[
          requiredRule,
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
    </>
  );
}
