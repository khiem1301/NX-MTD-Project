import { fill, formatNumber } from '@/utils/format';
import { LABELS } from '../constants';
import type { FabricMarkerPlan, FabricMarkerUse, WorkOrderFabricCheck } from '../types';

/** Tỉ lệ size của bìa theo thứ tự size máy chủ trả: "1M, 2L, 1XL" (số sản phẩm trước, size sau). */
export const ratioLabel = (use: FabricMarkerUse) =>
  use.ratio
    .map((r) => fill(LABELS.ratioPart, r.sizeCode, r.piecesPerPly))
    .join(LABELS.listSeparator);

/** "1A (1S, 2M, 1L) × 10 lớp". */
export const markerUseLabel = (use: FabricMarkerUse) =>
  fill(LABELS.markerUse, use.markerCode, ratioLabel(use), use.plies);

/** "Nhóm bìa 1: 1A (1S, 2M, 1L) × 10 lớp, 1B (4M) × 2 lớp (dư 16)"; cắt lẻ / cắt bù ghi riêng; chưa chắc tối ưu thì ghi chú. */
export const planLabel = (plan: FabricMarkerPlan) => {
  const uses = plan.markers.map(markerUseLabel).join(LABELS.listSeparator);
  const notes =
    (plan.surplus > 0 ? fill(LABELS.planSurplus, plan.surplus) : '') +
    (plan.isProvenOptimal ? '' : LABELS.planNotProven);
  return fill(plan.isExtra ? LABELS.planExtra : LABELS.planGroup, plan.markerNo, uses, notes);
};

/** Tên vải đầy đủ: mã, tên, màu. */
export const fabricName = (c: WorkOrderFabricCheck) =>
  `${c.materialCode} ${c.materialName}${c.materialColor ? ` (${c.materialColor})` : ''}`;

/** "V01 Vải gió (Đen): cần khoảng 195,3 m. Nhóm bìa 1: …; Nhóm bìa 2: …"; null khi vải chưa tính được. */
export const fabricPlanLine = (c: WorkOrderFabricCheck): string | null =>
  c.needMeters === null || !c.markerPlans?.length
    ? null
    : fill(
        LABELS.fabricPlanRow,
        fabricName(c),
        formatNumber(c.needMeters),
        c.markerPlans.map(planLabel).join(LABELS.planSeparator),
      );
