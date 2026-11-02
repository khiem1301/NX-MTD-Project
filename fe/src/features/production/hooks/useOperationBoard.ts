import { useState } from 'react';
import { useTableQuery } from '@/hooks/useTableQuery';
import type { OperationFilter } from '../utils/operationBoard';

const NO_FILTER: OperationFilter = { keyword: undefined, groupId: undefined };

/** Mã hàng, bộ lọc và các dòng đã tích của màn công đoạn. Đổi mã hàng hoặc bộ lọc thì bỏ chọn, để không xếp nhầm công đoạn đang bị ẩn. */
export function useOperationBoard() {
  const [productId, setProductId] = useState<number | null>(null);
  const [selected, setSelected] = useState<number[]>([]);
  const { filters, setFilters, pagination } = useTableQuery<OperationFilter>(NO_FILTER);

  return {
    productId,
    selected,
    setSelected,
    filters,
    pagination,
    changeProduct: (id: number | undefined) => {
      setProductId(id ?? null);
      setFilters(NO_FILTER);
      setSelected([]);
    },
    changeFilter: (change: OperationFilter) => {
      setFilters(change);
      setSelected([]);
    },
  };
}
