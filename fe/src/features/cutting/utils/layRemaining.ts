import type { FabricRequirement } from '@/types/workOrder';
import { lineKey } from '../constants';
import type { CuttingLay, CuttingLayFabric, CuttingLayLine, WorkOrderLine } from '../types';

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

/** Vải bàn phải nhập: hợp vải của các dòng có số lượng sau cắt (DEV-ALL-93), giữ thứ tự xuất hiện. */
export const requiredFabricIds = (
  lines: readonly WorkOrderLine[],
  qty: Record<string, number | null | undefined>,
) => [
  ...new Set(
    lines.filter((l) => (qty[lineKey(l)] ?? 0) > 0).flatMap((l) => l.fabricMaterialIds ?? []),
  ),
];

/** "V01: 66 m; V02: 10 m" — bàn cũ chưa có số mét thì ghi "—". */
export const formatLayFabrics = (
  fabrics: readonly CuttingLayFabric[],
  format: (v: number) => string,
) =>
  fabrics.map((f) => `${f.materialCode}: ${f.usedM === null ? '—' : format(f.usedM)}`).join('; ') ||
  '—';

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
 * Một vải của tổ trên đơn cho bàn đang nhập (DEV-ALL-90, DEV-ALL-93), cùng cách tính với BE: còn lại = tổ đang giữ − các bàn khác
 * đã dùng. `cutQty` của lệnh gồm cả bàn đang sửa (nếu bàn đó có vải này) nên trừ phần của bàn đó ra.
 */
export const teamFabricBalance = (
  fabric: FabricRequirement,
  editing: Pick<CuttingLay, 'fabrics'> | null,
  thisLayM: number,
): TeamFabricBalance => {
  const own = editing?.fabrics.find((f) => f.materialId === fabric.materialId)?.usedM ?? 0;
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
