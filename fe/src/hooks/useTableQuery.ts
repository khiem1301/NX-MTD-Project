import { useState } from 'react';
import { DEFAULT_PAGE_SIZE } from '@/constants/pagination';
import { TABLE_SORT, type ServerSort, type SortablePagination } from '@/utils/tableSort';

/** Trạng thái phân trang server-side + bộ lọc của một bảng; đổi bộ lọc thì quay về trang 1. */
export const useTableQuery = <F extends object>(initialFilters: F) => {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [filters, setFiltersState] = useState<F>(initialFilters);
  const [sort, setSortState] = useState<ServerSort>({});

  const setFilters = (patch: Partial<F>) => {
    setFiltersState((current) => ({ ...current, ...patch }));
    setPage(1);
  };

  // Đổi cột sắp xếp thì quay về trang 1.
  const setSort = (next: ServerSort) => {
    setSortState(next);
    setPage(1);
  };

  const pagination = (total: number | undefined): SortablePagination => ({
    current: page,
    pageSize,
    total: total ?? 0,
    showSizeChanger: true,
    onChange: (nextPage, nextSize) => {
      setPage(nextSize === pageSize ? nextPage : 1);
      setPageSize(nextSize);
    },
    [TABLE_SORT]: { sort, onChange: setSort },
  });

  return { params: { ...filters, page, pageSize, ...sort }, filters, setFilters, pagination };
};
