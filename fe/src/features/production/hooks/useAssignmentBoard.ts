import { useMemo, useState } from 'react';
import { useTableQuery } from '@/hooks/useTableQuery';
import {
  ASSIGNMENT_STATE,
  assignmentProducts,
  buildAssignmentRows,
  filterAssignmentRows,
  type AssignmentFilter,
} from '../utils/assignmentBoard';
import { useAllottedOrderLines, useAssignableOperations, useAssignments } from './useAssignments';
import { useWorkerOptions } from './useWorkerOptions';

export type AssignmentView = 'operation' | 'worker';

const NO_FILTER: AssignmentFilter = {
  keyword: undefined,
  group: undefined,
  workerId: undefined,
  state: ASSIGNMENT_STATE.ALL,
};

/** Dữ liệu và trạng thái màn phân công: đơn, mã hàng đang làm, bộ lọc, các dòng đã tích. */
export function useAssignmentBoard() {
  const [orderId, setOrderId] = useState<number | null>(null);
  const [productId, setProductId] = useState<number | null>(null);
  const [view, setView] = useState<AssignmentView>('operation');
  const { filters: filter, setFilters, pagination } = useTableQuery<AssignmentFilter>(NO_FILTER);
  const [selected, setSelected] = useState<number[]>([]);
  const assignments = useAssignments(orderId);
  const operations = useAssignableOperations(orderId);
  const lines = useAllottedOrderLines(orderId);
  const workers = useWorkerOptions();

  const allRows = useMemo(
    () =>
      buildAssignmentRows(
        operations.data ?? [],
        assignments.data ?? [],
        new Set(workers.options.map((o) => o.value)),
      ),
    [operations.data, assignments.data, workers.options],
  );
  // Điều kiện nhận BTP tính theo mã hàng trên phiếu nên mỗi lần chỉ làm một mã, tiến độ cũng theo mã đó.
  const products = useMemo(
    () => assignmentProducts(allRows, lines.data ?? []),
    [allRows, lines.data],
  );
  const current =
    products.find((p) => p.productId === productId) ??
    products.find((p) => p.total > 0) ??
    products[0];
  // Chỉ kết luận khi đã tải xong công đoạn, nếu không mọi mã đều trông như chưa khai.
  const noOperations = operations.isSuccess
    ? products.filter((p) => p.total === 0).map((p) => p.productCode)
    : [];
  const rows = allRows.filter((r) => r.operation.productId === current?.productId);
  const missing = rows.filter((r) => !r.isCovered).length;
  const visible = filterAssignmentRows(rows, filter);
  // Dòng đã tích có thể biến khỏi bảng (vừa có người khi lọc dòng thiếu, công đoạn ngừng dùng): không giao các dòng đó.
  const visibleIds = new Set(visible.map((r) => r.operation.operationId));
  const picked = selected.filter((id) => visibleIds.has(id));

  const changeView = (next: AssignmentView) => {
    setView(next);
    setSelected([]);
  };
  const changeOrder = (id: number | undefined) => {
    setOrderId(id ?? null);
    setProductId(null);
    setFilters(NO_FILTER);
    setSelected([]);
  };
  const changeProduct = (id: number) => {
    setProductId(id);
    setFilters(NO_FILTER);
    setSelected([]);
  };
  // Đổi bộ lọc thì bỏ chọn, để không giao nhầm công đoạn đang bị ẩn.
  const changeFilter = (change: Partial<AssignmentFilter>) => {
    setFilters(change);
    setSelected([]);
  };

  return {
    orderId,
    view,
    filter,
    pagination,
    picked,
    setSelected,
    assignments,
    operations,
    lines,
    workers,
    products,
    current,
    noOperations,
    rows,
    missing,
    visible,
    loading: operations.isLoading || assignments.isLoading,
    changeView,
    changeOrder,
    changeProduct,
    changeFilter,
  };
}
