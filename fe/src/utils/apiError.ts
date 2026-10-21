import type { FormInstance } from 'antd';
import type { ApiError } from '@/types/api';

export const isApiError = (value: unknown): value is ApiError =>
  typeof value === 'object' && value !== null && 'success' in value && value.success === false && 'errorCode' in value;

/** Lỗi 400 có errors theo field → hiện dưới ô nhập. Trả true nếu đã gắn được vào form. */
export const applyFieldErrors = (form: FormInstance, error: ApiError): boolean => {
  if (!error.errors) return false;
  form.setFields(Object.entries(error.errors).map(([name, errors]) => ({ name, errors })));
  return true;
};
