import { fill, formatQty } from '@/utils/format';
import { LABELS } from '../constants';

/** Giới hạn khớp OrderMaterialInputValidator ở BE (cột DECIMAL(18,3) và SMALLINT). */
export const MAX_QTY = 100_000_000;
export const QTY_DECIMALS = 3;
export const MAX_DELIVERY_COUNT = 99;
export const MAX_NOTE_LENGTH = 500;

const hasValidScale = (value: number): boolean =>
  Number.isInteger(Number((value * 10 ** QTY_DECIMALS).toFixed(6)));

/** Lỗi của một số lượng; null nếu hợp lệ. Số lượng không được âm, tối đa 100.000.000 và 3 số lẻ. */
export const qtyProblem = (label: string, value: number | null | undefined): string | null => {
  if (value === null || value === undefined) return null;
  if (!Number.isFinite(value)) return fill(LABELS.qtyMax, label, formatQty(MAX_QTY));
  if (value < 0) return fill(LABELS.qtyNegative, label);
  if (value > MAX_QTY) return fill(LABELS.qtyMax, label, formatQty(MAX_QTY));
  if (!hasValidScale(value)) return fill(LABELS.qtyScale, label, QTY_DECIMALS);
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
