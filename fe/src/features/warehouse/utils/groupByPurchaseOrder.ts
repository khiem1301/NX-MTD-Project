/** Gộp các dòng NVL của cùng một đơn mua thành một hàng: thông tin đơn lấy từ dòng đầu, các dòng NVL nằm trong `lines`. */
export type PurchaseOrderGroup<T> = T & { lines: T[] };

export const groupByPurchaseOrder = <T extends { purchaseOrderId: number }>(
  rows: readonly T[] | undefined,
): PurchaseOrderGroup<T>[] => {
  const groups = new Map<number, PurchaseOrderGroup<T>>();
  for (const row of rows ?? []) {
    const group = groups.get(row.purchaseOrderId);
    if (group) group.lines.push(row);
    else groups.set(row.purchaseOrderId, { ...row, lines: [row] });
  }
  return [...groups.values()];
};
