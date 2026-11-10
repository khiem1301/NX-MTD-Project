import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import type { NxtReportQuery, StockCardQuery } from '../types';
import { WAREHOUSE_KEY } from './useMaterials';

export const useStockCard = (query: StockCardQuery | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'stock-card', role, query],
    queryFn: () => warehouseApi.getStockCard(role, query ?? { warehouseType: '' }),
    enabled: query !== undefined,
  });
};

export const useNxtReport = (query: NxtReportQuery | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'nxt-report', role, query],
    queryFn: () => warehouseApi.getNxtReport(role, query!),
    enabled: query !== undefined,
    placeholderData: keepPreviousData,
  });
};

export const useTeamHoldings = (orderId?: number) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'team-holdings', role, orderId],
    queryFn: () => warehouseApi.getTeamHoldings(role, orderId),
  });
};
