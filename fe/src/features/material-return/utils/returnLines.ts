import { FABRIC_UNIT } from '@/constants/materials';
import { MATERIAL_QTY_DECIMALS } from '@/constants/quantity';
import { SCRAP_DECIMAL_UNIT } from '../constants';
import type {
  CreateReturnPayload,
  ReturnableLine,
  ReturnableQuery,
  ReturnWarehouse,
} from '../types';

export type ScrapRow = { materialId?: number; quantity?: number };

type ItemKey = Pick<ReturnableLine, 'materialId' | 'orderLineId'>;

/** Khóa dòng trong form: NVL theo mã NVL, BTP theo dòng đơn. */
export const lineKey = (l: ItemKey): string =>
  l.materialId !== null ? `m${l.materialId}` : `o${l.orderLineId}`;

/** Vải (m) và phế phẩm tính theo kg nhập được số lẻ (tối đa 3 chữ số); đơn vị khác (cái, bộ, cuộn…) là số nguyên. */
export const decimalsOfUnit = (unit: string): number =>
  [FABRIC_UNIT, SCRAP_DECIMAL_UNIT].includes(unit.trim().toLowerCase()) ? MATERIAL_QTY_DECIMALS : 0;

/** Tổ cắt: điền sẵn số vải còn lại sau cắt của lệnh cho các dòng đã tính được số đã cắt. */
export const suggestedQtys = (
  lines: ReturnableLine[],
  byWorkOrder: boolean,
): Record<string, number> =>
  Object.fromEntries(
    lines
      .filter((l) => byWorkOrder && l.usedQty !== null && l.remainingQty > 0)
      .map((l) => [lineKey(l), l.remainingQty]),
  );

/** Dòng trả gửi lên BE: hàng còn trả được có nhập số, cộng các dòng phế phẩm đã chọn. */
export const buildReturnLines = (
  qty: Record<string, number | null> | undefined,
  scrap: ScrapRow[] | undefined,
  returnable: ReturnableLine[],
): CreateReturnPayload['lines'] => [
  ...returnable
    .filter((l) => (qty?.[lineKey(l)] ?? 0) > 0)
    .map((l) => ({
      materialId: l.materialId ?? undefined,
      orderLineId: l.orderLineId ?? undefined,
      returnedQty: qty?.[lineKey(l)] ?? 0,
    })),
  ...(scrap ?? [])
    .filter((s) => s.materialId !== undefined && (s.quantity ?? 0) > 0)
    .map((s) => ({ materialId: s.materialId, returnedQty: s.quantity ?? 0 })),
];

type QueryScope = {
  kind: ReturnWarehouse;
  isCutting: boolean;
  workOrderId?: number;
  orderId?: number;
};

/**
 * Điều kiện tải hàng còn trả được: tổ cắt trả NVL phải chọn lệnh cắt, tổ may trả NVL không cần chọn gì, trả BTP phải chọn đơn hàng.
 * undefined = chưa đủ lựa chọn nên chưa tải.
 */
export const returnableQueryOf = ({
  kind,
  isCutting,
  workOrderId,
  orderId,
}: QueryScope): ReturnableQuery | undefined => {
  if (kind === 'SEMI_FINISHED')
    return orderId === undefined ? undefined : { warehouseType: kind, orderId };
  if (!isCutting) return { warehouseType: kind };
  return workOrderId === undefined ? undefined : { warehouseType: kind, workOrderId };
};
