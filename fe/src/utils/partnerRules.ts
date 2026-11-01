import { z } from 'zod';
import { MESSAGES } from '@/constants/messages';
import { EMAIL_INVALID_MESSAGE, EMAIL_PATTERN } from './email';
import { PHONE_INVALID_MESSAGE, PHONE_PATTERN } from './phone';
import { zodRule } from './zodRule';

/** Giới hạn khớp SaveCustomerDto / SaveSupplierDto ở BE và độ rộng cột database. */
export const PARTNER_LIMITS = {
  name: 200,
  email: 100,
  address: 500,
  materialTypes: 200,
  note: 500,
} as const;

const tooLong = (label: string, max: number) => `${label} tối đa ${max} ký tự.`;

/** Rule cho khách hàng / nhà cung cấp (Rules 4.9): độ dài tính sau khi cắt khoảng trắng đầu cuối, đúng với giá trị BE lưu. */
export const partnerRules = {
  required: (label: string, max: number, requiredMessage: string = MESSAGES.common.required) =>
    zodRule(z.string().trim().min(1, requiredMessage).max(max, tooLong(label, max))),
  optional: (label: string, max: number) =>
    zodRule(z.string().trim().max(max, tooLong(label, max))),
  phone: zodRule(
    z.string().trim().min(1, MESSAGES.common.required).regex(PHONE_PATTERN, PHONE_INVALID_MESSAGE),
  ),
  email: zodRule(
    z
      .string()
      .trim()
      .min(1, MESSAGES.common.required)
      .max(PARTNER_LIMITS.email, tooLong('Email', PARTNER_LIMITS.email))
      .regex(EMAIL_PATTERN, EMAIL_INVALID_MESSAGE),
  ),
};
