import type { ConfirmBatchItem, WorkAllocation } from '../types';

/** Số xác nhận và lý do lệch công nhân nhập cho từng phiếu (chưa nhập = lấy số giao, không lý do). */
export type BatchEdit = { confirmedQty?: number | null; discrepancyReason?: string };
export type BatchEdits = Record<number, BatchEdit>;

export const batchQty = (m: WorkAllocation, edits: BatchEdits): number | null => {
  const qty = edits[m.workAllocationId]?.confirmedQty;
  return qty === undefined ? m.quantity : qty;
};

/** Phiếu nhận khác số giao mà chưa có lý do (BE cũng chặn, CK_WA_Confirmed). */
export const needsReason = (m: WorkAllocation, edits: BatchEdits): boolean =>
  batchQty(m, edits) !== m.quantity && !edits[m.workAllocationId]?.discrepancyReason?.trim();

/** Điền lý do chung cho mọi phiếu lệch số chưa có lý do; không đè lý do đã nhập. */
export function applySharedReason(
  rows: WorkAllocation[],
  edits: BatchEdits,
  reason: string,
): BatchEdits {
  const text = reason.trim();
  if (!text) return edits;
  const next = { ...edits };
  rows
    .filter((m) => needsReason(m, edits))
    .forEach((m) => {
      next[m.workAllocationId] = { ...next[m.workAllocationId], discrepancyReason: text };
    });
  return next;
}

/** Danh sách gửi BE; trả null khi còn phiếu thiếu số hoặc lệch số chưa có lý do. */
export function buildBatchItems(
  rows: WorkAllocation[],
  edits: BatchEdits,
): ConfirmBatchItem[] | null {
  const invalid = rows.some((m) => {
    const qty = batchQty(m, edits);
    return !qty || qty < 1 || qty > m.quantity || needsReason(m, edits);
  });
  if (invalid) return null;
  return rows.map((m) => {
    const qty = batchQty(m, edits) ?? m.quantity;
    return {
      workAllocationId: m.workAllocationId,
      confirmedQty: qty,
      discrepancyReason:
        qty === m.quantity ? undefined : edits[m.workAllocationId]?.discrepancyReason?.trim(),
    };
  });
}
