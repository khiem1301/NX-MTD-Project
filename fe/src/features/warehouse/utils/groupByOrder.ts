/** Gộp các dòng NVL khách cấp của cùng một đơn hàng thành một hàng: thông tin đơn lấy từ dòng đầu, các dòng nằm trong `lines`. */
export type OrderGroup<T> = T & { lines: T[] };

export const groupByOrder = <T extends { orderId: number }>(
  rows: readonly T[] | undefined,
): OrderGroup<T>[] => {
  const groups = new Map<number, OrderGroup<T>>();
  for (const row of rows ?? []) {
    const group = groups.get(row.orderId);
    if (group) group.lines.push(row);
    else groups.set(row.orderId, { ...row, lines: [row] });
  }
  return [...groups.values()];
};
