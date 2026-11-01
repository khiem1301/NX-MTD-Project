import type { SaveWorkOrderLine, WorkOrderLine } from '../types';

/** Một dòng cắt lẻ thêm thành phần trên form; đang nhập dở thì các ô có thể trống. */
export type ExtraComponentInput = {
  orderLineId?: number;
  productComponentMaterialId?: number;
  plannedQty?: number | null;
};

/** Cả bộ: chỉ giữ dòng có số kế hoạch. Cắt lẻ: chỉ giữ dòng đã chọn đủ màu – size, thành phần và có số chi tiết. */
export const toWorkOrderLines = (
  planned: Record<string, number | null> | undefined,
  extras?: (ExtraComponentInput | undefined)[],
): SaveWorkOrderLine[] => [
  ...Object.entries(planned ?? {})
    .filter(([, qty]) => (qty ?? 0) > 0)
    .map(([orderLineId, qty]) => ({ orderLineId: Number(orderLineId), plannedQty: qty ?? 0 })),
  ...(extras ?? []).flatMap((e) =>
    e?.orderLineId && e.productComponentMaterialId && (e.plannedQty ?? 0) > 0
      ? [
          {
            orderLineId: e.orderLineId,
            productComponentMaterialId: e.productComponentMaterialId,
            plannedQty: e.plannedQty ?? 0,
          },
        ]
      : [],
  ),
];

/** Có dòng cắt lẻ khai hai lần cho cùng màu – size và chi tiết (BE từ chối vì trùng dòng). */
export const hasDuplicateLines = (lines: SaveWorkOrderLine[]): boolean => {
  const keys = lines.map((l) => `${l.orderLineId}|${l.productComponentMaterialId ?? ''}`);
  return new Set(keys).size !== keys.length;
};

/** Tách dòng của lệnh đã lưu để điền lại form: cả bộ theo dòng đơn, các dòng còn lại là cắt lẻ. */
export const splitWorkOrderLines = (lines: WorkOrderLine[]) => ({
  planned: Object.fromEntries(
    lines
      .filter((l) => l.productComponentMaterialId === null)
      .map((l) => [String(l.orderLineId), l.plannedQty]),
  ),
  extras: lines
    .filter((l) => l.productComponentMaterialId !== null)
    .map<ExtraComponentInput>((l) => ({
      orderLineId: l.orderLineId,
      productComponentMaterialId: l.productComponentMaterialId ?? undefined,
      plannedQty: l.plannedQty,
    })),
});
