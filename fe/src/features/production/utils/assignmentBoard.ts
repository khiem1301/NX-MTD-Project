import { foldVietnamese } from '@/utils/colorSearch';
import type { OperationAssignment, OperationOption } from '../types';

/** Một dòng của bảng phân công: công đoạn và các công nhân đang được phân công. */
export type AssignmentRow = {
  operation: OperationOption;
  assignments: OperationAssignment[];
  /** Có ít nhất một công nhân đang làm của tổ; khớp điều kiện nhận BTP ở BE. */
  isCovered: boolean;
};

export const buildAssignmentRows = (
  operations: readonly OperationOption[],
  assignments: readonly OperationAssignment[],
  activeWorkerIds: ReadonlySet<number>,
): AssignmentRow[] =>
  operations.map((operation) => {
    const own = assignments.filter((a) => a.operationId === operation.operationId);
    return {
      operation,
      assignments: own,
      isCovered: own.some((a) => activeWorkerIds.has(a.workerId)),
    };
  });

export type AssignmentProduct = {
  productId: number;
  productCode: string;
  total: number;
  covered: number;
};

/**
 * Các mã hàng trong bảng phân công và số công đoạn đã có người của từng mã. Mã được chia cho tổ mà chưa
 * có công đoạn nào không xuất hiện trong `rows`, nên lấy thêm từ `allotted` với tổng bằng 0.
 */
export const assignmentProducts = (
  rows: readonly AssignmentRow[],
  allotted: readonly Pick<AssignmentProduct, 'productId' | 'productCode'>[] = [],
): AssignmentProduct[] => {
  const products = new Map<number, AssignmentProduct>();
  for (const { productId, productCode } of allotted)
    products.set(productId, { productId, productCode, total: 0, covered: 0 });
  for (const { operation: o, isCovered } of rows) {
    const product = products.get(o.productId) ?? {
      productId: o.productId,
      productCode: o.productCode,
      total: 0,
      covered: 0,
    };
    product.total += 1;
    if (isCovered) product.covered += 1;
    products.set(o.productId, product);
  }
  return [...products.values()];
};

export const ASSIGNMENT_STATE = { ALL: 'all', MISSING: 'missing', COVERED: 'covered' } as const;
export type AssignmentState = (typeof ASSIGNMENT_STATE)[keyof typeof ASSIGNMENT_STATE];

export type AssignmentFilter = {
  keyword?: string;
  group?: string;
  workerId?: number;
  state: AssignmentState;
};

export const filterAssignmentRows = (
  rows: readonly AssignmentRow[],
  { keyword, group, workerId, state }: AssignmentFilter,
): AssignmentRow[] => {
  const kw = foldVietnamese(keyword?.trim() ?? '');
  return rows.filter(({ operation: o, assignments, isCovered }) => {
    if (state === ASSIGNMENT_STATE.MISSING && isCovered) return false;
    if (state === ASSIGNMENT_STATE.COVERED && !isCovered) return false;
    if (group !== undefined && o.groupName !== group) return false;
    if (workerId !== undefined && !assignments.some((a) => a.workerId === workerId)) return false;
    return !kw || foldVietnamese(`${o.operationCode} ${o.operationName}`).includes(kw);
  });
};

/** Các nhóm công đoạn có trong đơn, theo thứ tự xuất hiện. */
export const assignmentGroups = (rows: readonly AssignmentRow[]): string[] => [
  ...new Set(rows.flatMap((r) => (r.operation.groupName ? [r.operation.groupName] : []))),
];

/** Số công đoạn của từng công nhân, để chia việc cho đều. */
export const workerLoads = (rows: readonly AssignmentRow[]): Map<number, number> => {
  const loads = new Map<number, number>();
  for (const row of rows)
    for (const a of row.assignments) loads.set(a.workerId, (loads.get(a.workerId) ?? 0) + 1);
  return loads;
};
