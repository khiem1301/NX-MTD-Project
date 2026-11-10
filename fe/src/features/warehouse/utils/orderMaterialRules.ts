import { MATERIAL_QTY_DECIMALS, MAX_MATERIAL_QTY } from '@/constants/quantity';
import { fill, formatQty } from '@/utils/format';
import type { MaterialOption } from '@/types/lookup';
import { LABELS } from '../constants';
import type { OrderMaterial } from '../types';

/** Giới hạn khớp OrderMaterialInputValidator ở BE (cột SMALLINT); số lượng dùng constants/quantity. */
export const MAX_DELIVERY_COUNT = 99;
export const MAX_NOTE_LENGTH = 500;

const hasValidScale = (value: number): boolean =>
  Number.isInteger(Number((value * 10 ** MATERIAL_QTY_DECIMALS).toFixed(6)));

/** Lỗi của một số lượng; null nếu hợp lệ. Số lượng không được âm, tối đa 100.000.000 và 3 số lẻ. */
export const qtyProblem = (label: string, value: number | null | undefined): string | null => {
  if (value === null || value === undefined) return null;
  if (!Number.isFinite(value)) return fill(LABELS.qtyMax, label, formatQty(MAX_MATERIAL_QTY));
  if (value < 0) return fill(LABELS.qtyNegative, label);
  if (value > MAX_MATERIAL_QTY) return fill(LABELS.qtyMax, label, formatQty(MAX_MATERIAL_QTY));
  if (!hasValidScale(value)) return fill(LABELS.qtyScale, label, MATERIAL_QTY_DECIMALS);
  return null;
};

/** Số lần giao dự kiến: số nguyên từ 1 đến 99; chỉ kiểm khi đã nhập (bắt buộc hay không do khách có cấp hay không). */
export const deliveryCountProblem = (value: number | null | undefined): string | null => {
  if (value === null || value === undefined) return null;
  return Number.isInteger(value) && value >= 1 && value <= MAX_DELIVERY_COUNT
    ? null
    : fill(LABELS.deliveryCountRange, MAX_DELIVERY_COUNT);
};

/** Ghi chú tối đa 500 ký tự, tính sau khi cắt khoảng trắng đầu cuối (khớp BE). */
export const noteProblem = (value: string | null | undefined): string | null =>
  (value ?? '').trim().length > MAX_NOTE_LENGTH ? fill(LABELS.noteMax, MAX_NOTE_LENGTH) : null;

/**
 * NVL chọn được cho một dòng NVL của đơn: bỏ NVL đơn đã khai báo (mỗi NVL một dòng), trừ NVL của chính dòng đang sửa;
 * NVL đang sửa đã ngừng dùng (không còn trong danh mục) vẫn hiện để giữ giá trị.
 */
export const orderMaterialOptions = (
  materials: readonly MaterialOption[],
  orderMaterial: OrderMaterial | null,
  declared: readonly OrderMaterial[],
) => {
  const options = materials
    .filter(
      (m) =>
        m.materialId === orderMaterial?.materialId ||
        !declared.some((om) => om.materialId === m.materialId),
    )
    .map((m) => ({
      value: m.materialId,
      label: `${m.materialCode} - ${m.materialName}${m.color ? ` - ${m.color}` : ''} (${m.unit})`,
    }));
  if (orderMaterial && !options.some((o) => o.value === orderMaterial.materialId)) {
    options.push({
      value: orderMaterial.materialId,
      label: `${orderMaterial.materialCode} - ${orderMaterial.materialName}${orderMaterial.materialColor ? ` - ${orderMaterial.materialColor}` : ''}`,
    });
  }
  return options;
};
