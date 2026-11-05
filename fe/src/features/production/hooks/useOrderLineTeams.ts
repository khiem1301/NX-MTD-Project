import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { productionApi } from '../api/productionApi';
import { PRODUCTION_TEXT } from '../constants';
import { orderLineTeamsKey } from './productionKeys';
import type {
  AddOrderLineTeamsPayload,
  SetOrderLineTeamsPayload,
  OrderLineTeamParams,
} from '../types';

// ---------- Chia mã hàng của đơn cho tổ may ----------

export const useOrderLineTeams = (params: OrderLineTeamParams) =>
  useQuery({
    queryKey: [...orderLineTeamsKey, params],
    queryFn: () => productionApi.listOrderLineTeams(params),
  });

export const useSetOrderLineTeams = () =>
  useApiMutation((body: SetOrderLineTeamsPayload) => productionApi.setOrderLineTeams(body), {
    invalidate: [orderLineTeamsKey],
    successMessage: PRODUCTION_TEXT.orderLineTeams.saved,
  });

export const useAddOrderLineTeams = () =>
  useApiMutation((body: AddOrderLineTeamsPayload) => productionApi.addOrderLineTeams(body), {
    invalidate: [orderLineTeamsKey],
  });
