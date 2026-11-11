import type { WorkOrderMaterialAllowance } from '@/types/workOrder';
import { FABRIC_QTY_DECIMALS } from '../constants';

const SCALE = 10 ** FABRIC_QTY_DECIMALS;
const roundDown = (value: number) => Math.floor(value * SCALE + 1e-9) / SCALE;

/**
 * Dòng gợi ý khi chọn lệnh cắt: mỗi vải hệ thống tính được, xin phần còn thiếu so với số hệ thống tính
 * (cần − đã xin), không quá số còn xin được theo trần. Vải đã xin đủ hoặc chưa tính được thì bỏ.
 */
export const suggestLines = (allowance: readonly WorkOrderMaterialAllowance[]) =>
  allowance
    .filter((a) => a.needQty !== null && a.remainingQty !== null)
    .map((a) => ({
      materialId: a.materialId,
      quantity: roundDown(Math.min(a.remainingQty ?? 0, (a.needQty ?? 0) - a.requestedQty)),
    }))
    .filter((l) => l.quantity > 0);

/** Số còn xin được của một vải theo lệnh; undefined = vải không xin theo lệnh được. */
export const remainingOf = (
  allowance: readonly WorkOrderMaterialAllowance[] | undefined,
  materialId: number | undefined,
) => allowance?.find((a) => a.materialId === materialId)?.remainingQty ?? undefined;
