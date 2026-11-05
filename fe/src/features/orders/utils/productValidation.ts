import { z } from 'zod';
import { MESSAGES } from '@/constants/messages';
import { fill } from '@/utils/format';
import { COLOR_MAX_LENGTH, MAX_COLOR_IMAGES, MIN_COLOR_IMAGES } from '../constants';
import type { ProductColorImages } from '../types';

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

/** Tên lỗi BE trả về (vd "colorImages[0].Images") → đường dẫn field của Form antd (["colorImages", 0, "images"]). */
export function toFieldPath(name: string): (string | number)[] {
  return name
    .split(/[.[\]]/)
    .filter(Boolean)
    .map((part) =>
      /^\d+$/.test(part) ? Number(part) : part.charAt(0).toLowerCase() + part.slice(1),
    );
}

const colorKey = (color: string | undefined) => (color ?? '').trim().toLowerCase();

/**
 * Lỗi tên màu của dòng thứ `index` trong ảnh theo màu (khớp ProductColorImagesDtoValidator ở BE): bắt buộc, tối đa 50 ký tự,
 * không trùng màu ở dòng trên (không phân biệt hoa thường, bỏ khoảng trắng). null = hợp lệ.
 */
export function colorNameError(
  rows: readonly (ProductColorImages | undefined)[] | undefined,
  index: number,
): string | null {
  const color = rows?.[index]?.color?.trim() ?? '';
  if (!color) return text.colorRequired;
  if (color.length > COLOR_MAX_LENGTH) return fill(text.colorMax, COLOR_MAX_LENGTH);
  const duplicate = (rows ?? []).some(
    (r, i) => i < index && colorKey(r?.color) === colorKey(color),
  );
  return duplicate ? fill(text.colorDuplicate, color) : null;
}

/** Lỗi số ảnh của một màu (1 đến 3). null = hợp lệ. */
export function colorImageCountError(images: readonly string[] | undefined): string | null {
  const count = images?.length ?? 0;
  return count < MIN_COLOR_IMAGES || count > MAX_COLOR_IMAGES
    ? fill(text.colorImageCount, MIN_COLOR_IMAGES, MAX_COLOR_IMAGES)
    : null;
}
