import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { stocktakeApi } from '../api/stocktakeApi';
import { ST_MESSAGES } from '../constants';
import type { SaveStocktakePayload, StocktakeQuery } from '../types';

export const STOCKTAKE_KEY = ['stocktake'] as const;
// Duyệt phiếu điều chỉnh tồn kho nên nạp lại cả nhóm warehouse.
const AFFECTED = [STOCKTAKE_KEY, ['warehouse']];

export const useStocktakes = (query: StocktakeQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...STOCKTAKE_KEY, 'list', role, query],
    queryFn: () => stocktakeApi.list(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useStocktake = (id: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...STOCKTAKE_KEY, 'detail', role, id],
    queryFn: () => stocktakeApi.detail(role, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useSaveStocktake = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, body }: { id?: number; body: SaveStocktakePayload }) =>
      id ? stocktakeApi.update(role, id, body) : stocktakeApi.create(role, body),
    { invalidate: AFFECTED, successMessage: ST_MESSAGES.saved },
  );
};

export const useApproveStocktake = () => {
  const role = useRolePath();
  return useApiMutation((id: number) => stocktakeApi.approve(role, id), {
    invalidate: AFFECTED,
    successMessage: ST_MESSAGES.approved,
  });
};

export const useReturnStocktake = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, reason }: { id: number; reason: string }) =>
      stocktakeApi.returnToKeeper(role, id, reason),
    { invalidate: AFFECTED, successMessage: ST_MESSAGES.returned },
  );
};
