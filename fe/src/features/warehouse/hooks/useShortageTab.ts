import { useState } from 'react';
import { useTableQuery } from '@/hooks/useTableQuery';
import { useShortDeliveries } from './useShortDeliveries';

/** Dữ liệu tab Giao bù của màn NVL chờ nhận: đơn thiếu NVL phân trang ở máy chủ, mặc định xem tất cả. */
export const useShortageTab = (keyword: string) => {
  const [awaitingOnly, setAwaitingOnly] = useState(false);
  const table = useTableQuery({});
  const search = keyword.trim() || undefined;
  const list = useShortDeliveries({
    ...table.params,
    keyword: search,
    awaitingReplenish: awaitingOnly || undefined,
  });
  return { list, table, search, awaitingOnly, setAwaitingOnly };
};
