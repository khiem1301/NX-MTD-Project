import { fill } from '@/utils/format';
import { KPI_TEXT } from '../constants';
import type { KpiLineCapacity, KpiTargetLine } from '../types';

/** Giá trị ô nhập chỉ tiêu theo màu × size trong form, khóa là orderLineId. */
export type LineQtyValues = Record<string, number | null | undefined>;

/** Lỗi của một ô: vượt số còn lại của dòng, hoặc nhỏ hơn số tổ đã làm được. Không lỗi thì undefined. */
export const lineQtyError = (line: KpiLineCapacity, qty: number | null | undefined) => {
  const value = qty ?? 0;
  if (value > line.maxTarget) return KPI_TEXT.form.lineExceeds;
  return value < line.actualQty ? fill(KPI_TEXT.form.lineBelowActual, line.actualQty) : undefined;
};

/** Các dòng có số > 0 để gửi BE; chỉ lấy dòng còn trong danh sách tổ được chia. */
export const toPayloadLines = (lines: KpiLineCapacity[], values: LineQtyValues | undefined) =>
  lines
    .map((l) => ({ orderLineId: l.orderLineId, targetQty: values?.[String(l.orderLineId)] ?? 0 }))
    .filter((l) => l.targetQty > 0);

export const sumLineQty = (lines: KpiLineCapacity[], values: LineQtyValues | undefined) =>
  toPayloadLines(lines, values).reduce((sum, l) => sum + l.targetQty, 0);

/** Giá trị ban đầu của form khi sửa chỉ tiêu: số đã giao theo từng màu × size. */
export const toLineQtyValues = (lines: KpiTargetLine[] | null): LineQtyValues =>
  Object.fromEntries((lines ?? []).map((l) => [String(l.orderLineId), l.targetQty]));
