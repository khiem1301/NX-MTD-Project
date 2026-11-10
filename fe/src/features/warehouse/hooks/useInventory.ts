import { useQuery } from '@tanstack/react-query';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import type { StockCardQuery } from '../types';
import { WAREHOUSE_KEY } from './useMaterials';

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
