import type { FormInstance } from 'antd';
import { ERROR_CODES } from '@/constants/errorCodes';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';

/** Lỗi đổi mật khẩu: sai mật khẩu hiện tại (409, MSG07) hiện ở ô đó; lỗi theo trường hiện ở từng ô; còn lại trả về false. */
export function showPasswordError(form: FormInstance, error: ApiError): boolean {
  if (error.errorCode === ERROR_CODES.AuthWrongCurrentPassword) {
    form.setFields([{ name: 'currentPassword', errors: [MESSAGES.hr.wrongCurrentPassword] }]);
    return true;
  }
  if (!error.errors) return false;
  form.setFields(Object.entries(error.errors).map(([name, errors]) => ({ name, errors })));
  return true;
}
