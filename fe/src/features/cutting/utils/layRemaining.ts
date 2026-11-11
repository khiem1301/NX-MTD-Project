import type { FabricRequirement } from '@/types/workOrder';
import { lineKey } from '../constants';
import type { CuttingLay, CuttingLayLine, WorkOrderLine } from '../types';

/** Số đã cắt của dòng ở các bàn khác: `cutQty` của lệnh cắt gồm cả bàn đang sửa nên trừ phần của bàn đó ra. */
export const cutBeforeOf = (line: WorkOrderLine, editingLines: readonly CuttingLayLine[] = []) =>
  line.cutQty -
  editingLines.filter((l) => lineKey(l) === lineKey(line)).reduce((sum, l) => sum + l.actualQty, 0);

/**
 * Số còn cần cắt sau bàn đang nhập: kế hoạch − đã cắt ở bàn khác − số lượng sau cắt của bàn này.
 * Âm là bàn này cắt vượt kế hoạch (BE còn cho vượt trong mức % dư, quá mức thì chặn).
 */
export const remainingToCut = (plannedQty: number, cutBefore: number, thisLayQty: number) =>
  plannedQty - cutBefore - thisLayQty;

export type TeamFabricBalance = {
  held: number;
  usedBefore: number;
  /** Vải còn lại trước bàn này, không âm; số mét của bàn không được vượt số này (BE chặn). */
  left: number;
  thisLay: number;
  after: number;
  unmeasuredLays: number;
};

/**
 * Vải của tổ trên đơn cho bàn đang nhập (DEV-ALL-90), cùng cách tính với BE: còn lại = tổ đang giữ − các bàn khác đã dùng.
 * `cutQty` của lệnh gồm cả bàn đang sửa (nếu cùng vải) nên trừ phần của bàn đó ra.
 */
export const teamFabricBalance = (
  fabric: FabricRequirement,
  editing: Pick<CuttingLay, 'fabricMaterialId' | 'fabricUsedM'> | null,
  thisLayM: number,
): TeamFabricBalance => {
  const own = editing?.fabricMaterialId === fabric.materialId ? (editing.fabricUsedM ?? 0) : 0;
  const usedBefore = fabric.cutQty - own;
  const left = Math.max(0, fabric.heldQty - usedBefore);
  return {
    held: fabric.heldQty,
    usedBefore,
    left,
    thisLay: thisLayM,
    after: left - thisLayM,
    unmeasuredLays: fabric.unmeasuredLayCount,
  };
};
