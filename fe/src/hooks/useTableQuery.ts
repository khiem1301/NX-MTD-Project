import { useState } from 'react';
import type { TablePaginationConfig } from 'antd';
import { DEFAULT_PAGE_SIZE } from '@/constants/pagination';

/** Trạng thái phân trang server-side + bộ lọc của một bảng; đổi bộ lọc thì quay về trang 1. */
export const useTableQuery = <F extends object>(initialFilters: F) => {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);
  const [filters, setFiltersState] = useState<F>(initialFilters);

  const setFilters = (patch: Partial<F>) => {
    setFiltersState((current) => ({ ...current, ...patch }));
    setPage(1);
  };

  const pagination = (total: number | undefined): TablePaginationConfig => ({
    current: page,
    pageSize,
    total: total ?? 0,
    showSizeChanger: true,
    onChange: (nextPage, nextSize) => {
      setPage(nextSize === pageSize ? nextPage : 1);
      setPageSize(nextSize);
    },
  });

  return { params: { ...filters, page, pageSize }, filters, setFilters, pagination };
};
