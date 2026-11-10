import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useRolePath } from '@/hooks/useRolePath';
import { stockApi } from '@/services/stockApi';
import type { StockBalanceQuery } from '@/types/stock';

/** Nằm dưới key 'warehouse' để mọi mutation làm mới kho cũng làm mới tồn. */
export const useBalances = (query: StockBalanceQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: ['warehouse', 'balances', role, query],
    queryFn: () => stockApi.getBalances(role, query),
    placeholderData: keepPreviousData,
  });
};
