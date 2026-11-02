type TotalLine = { orderedQty?: number | null; unitPrice?: number | null };

/** Tổng tiền đơn mua = Σ số mua × đơn giá; dòng chưa nhập đủ tính 0. Làm tròn 2 số lẻ như cột tiền ở BE. */
export const purchaseOrderTotal = (lines: readonly (TotalLine | undefined)[] | undefined): number =>
  Math.round(
    (lines ?? []).reduce((sum, l) => sum + (l?.orderedQty ?? 0) * (l?.unitPrice ?? 0), 0) * 100,
  ) / 100;
