import { useMemo, useState } from 'react';
import { DEFAULT_PAGE_SIZE } from '../constants';
import type { TimesheetFilter } from '../types';
import { defaultRange, toApiDate, type DateRange } from '../utils';

/** Gom state bộ lọc + phân trang của màn bảng công; đổi bộ lọc thì quay về trang 1. */
export const useTimesheetFilter = () => {
  const [range, setRange] = useState<DateRange>(defaultRange);
  const [keyword, setKeyword] = useState('');
  const [onlyMissingPunch, setOnlyMissingPunch] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  const filter = useMemo<TimesheetFilter>(
    () => ({
      fromDate: toApiDate(range[0]),
      toDate: toApiDate(range[1]),
      keyword: keyword.trim() || undefined,
      onlyMissingPunch: onlyMissingPunch || undefined,
      page,
      pageSize,
    }),
    [range, keyword, onlyMissingPunch, page, pageSize],
  );

  const reset = (): void => {
    setRange(defaultRange());
    setKeyword('');
    setOnlyMissingPunch(false);
    setPage(1);
  };

  return {
    filter,
    range,
    keyword,
    onlyMissingPunch,
    setRange: (value: DateRange) => {
      setRange(value);
      setPage(1);
    },
    setKeyword: (value: string) => {
      setKeyword(value);
      setPage(1);
    },
    setOnlyMissingPunch: (value: boolean) => {
      setOnlyMissingPunch(value);
      setPage(1);
    },
    changePage: (nextPage: number, nextSize: number) => {
      setPage(nextPage);
      setPageSize(nextSize);
    },
    reset,
  };
};
