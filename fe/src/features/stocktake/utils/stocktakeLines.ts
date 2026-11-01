import type { SaveStocktakePayload } from '../types';

/** Một phiếu tối đa 1.000 dòng (khớp SaveStocktakeDtoValidator ở BE). */
export const MAX_LINES = 1000;

/** Hàng hóa được kiểm kê: kho NVL xác định bằng NVL (đã gồm màu), kho BTP / thành phẩm bằng dòng đơn (+ chi tiết lẻ). */
export type LineTarget = {
  materialId: number | null;
  orderLineId: number | null;
  productComponentMaterialId: number | null;
};

export type CountValues = {
  counted?: Record<string, number | null | undefined>;
  notes?: Record<string, string | undefined>;
};

export const targetKey = (t: LineTarget): string =>
  t.materialId !== null
    ? `${t.materialId}`
    : `${t.orderLineId}:${t.productComponentMaterialId ?? ''}`;

/**
 * Các dòng gửi lên máy chủ: chỉ dòng có nhập số đếm hoặc ghi chú, dòng còn lại coi như khớp sổ. Duyệt theo danh sách
 * đã gặp (mọi trang đã mở) chứ không theo trang đang hiện, để số đã nhập ở trang khác không bị mất.
 */
export const toStocktakeLines = (
  values: CountValues,
  targets: ReadonlyMap<string, LineTarget>,
): SaveStocktakePayload['lines'] =>
  [...targets].flatMap(([key, t]) => {
    const countedQty = values.counted?.[key] ?? null;
    const note = values.notes?.[key]?.trim() || undefined;
    if (countedQty === null && note === undefined) return [];
    return [
      {
        materialId: t.materialId ?? undefined,
        orderLineId: t.orderLineId ?? undefined,
        productComponentMaterialId: t.productComponentMaterialId,
        countedQty,
        note,
      },
    ];
  });
