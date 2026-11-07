import { z } from 'zod';
import { MESSAGES } from '@/constants/messages';

const text = MESSAGES.orders.productValidation;

/** Giới hạn khớp SaveProductDtoValidator ở BE; FE chỉ báo sớm, BE vẫn kiểm lại. */
export const productCodeSchema = z
  .string()
  .trim()
  .min(1, MESSAGES.common.required)
  .max(30, text.codeMax)
  .regex(/^[A-Za-z0-9._-]+$/, text.codeFormat);

export const productNameSchema = z
  .string()
  .refine((v) => v.trim().length > 0, MESSAGES.common.required)
  .refine((v) => v.length <= 200, text.nameMax);

export const productDescriptionSchema = z.string().max(1000, text.descriptionMax);

/** Tên lỗi BE trả về (vd "ExtraImages[0]") → đường dẫn field của Form antd (["extraImages", 0]). */
export function toFieldPath(name: string): (string | number)[] {
  return name
    .split(/[.[\]]/)
    .filter(Boolean)
    .map((part) =>
      /^\d+$/.test(part) ? Number(part) : part.charAt(0).toLowerCase() + part.slice(1),
    );
}
