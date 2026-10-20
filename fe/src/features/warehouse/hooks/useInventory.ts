import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import type { StockBalanceQuery, StockCardQuery } from '../types';
import { WAREHOUSE_KEY } from './useMaterials';

export const useBalances = (query: StockBalanceQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'balances', role, query],
    queryFn: () => warehouseApi.getBalances(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useStockCard = (query: StockCardQuery | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'stock-card', role, query],
    queryFn: () => warehouseApi.getStockCard(role, query ?? { warehouseType: '' }),
    enabled: query !== undefined,
  });
};

export const useTeamHoldings = (orderId?: number) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'team-holdings', role, orderId],
    queryFn: () => warehouseApi.getTeamHoldings(role, orderId),
  });
};
