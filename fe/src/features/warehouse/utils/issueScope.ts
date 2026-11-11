/**
 * Hàng được chọn trên phiếu cấp (DEV-ALL-99), khớp kiểm ở BE (MaterialIssueService):
 * theo lệnh cắt → chỉ vải của lệnh; không theo lệnh → chỉ NVL của đơn hàng; có đơn xin → chỉ hàng trong đơn xin (và vẫn trong
 * phạm vi trên). Chưa chọn lệnh cắt / đơn hàng / đơn xin thì không chọn được gì.
 */
export const allowedIssueItems = ({
  requested,
  workOrderItems,
  orderItems,
}: {
  /** Hàng trong đơn xin (rỗng = không chọn đơn xin). */
  requested: ReadonlySet<number>;
  /** Vải của lệnh cắt (undefined = không theo lệnh). */
  workOrderItems?: readonly number[];
  /** NVL / dòng của đơn hàng (undefined = chưa chọn đơn hàng). */
  orderItems?: readonly number[];
}): Set<number> => {
  const scope = workOrderItems ?? orderItems;
  if (requested.size === 0) return new Set(scope ?? []);
  return new Set([...requested].filter((id) => scope === undefined || scope.includes(id)));
};
