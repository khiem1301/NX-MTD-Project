import { lineKey } from '../constants';
import type { CuttingLayLine, WorkOrderLine } from '../types';

/** Số đã cắt của dòng ở các bàn khác: `cutQty` của lệnh cắt gồm cả bàn đang sửa nên trừ phần của bàn đó ra. */
export const cutBeforeOf = (line: WorkOrderLine, editingLines: readonly CuttingLayLine[] = []) =>
  line.cutQty -
  editingLines.filter((l) => lineKey(l) === lineKey(line)).reduce((sum, l) => sum + l.actualQty, 0);

/**
 * Số còn cần cắt sau bàn đang nhập: kế hoạch − đã cắt ở bàn khác − số của bàn này (số thực tế nếu nhập, không thì số tính).
 * Âm là bàn này cắt vượt kế hoạch (BE còn cho vượt trong mức % dư, quá mức thì chặn).
 */
export const remainingToCut = (plannedQty: number, cutBefore: number, thisLayQty: number) =>
  plannedQty - cutBefore - thisLayQty;
