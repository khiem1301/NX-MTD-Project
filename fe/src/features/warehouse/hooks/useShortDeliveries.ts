import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import type {
  ResolveShortDeliveryPayload,
  SaveSupplementaryPayload,
  ShortDeliveryQuery,
} from '../types';
import { WAREHOUSE_KEY } from './useMaterials';

export const useShortDeliveries = (query: ShortDeliveryQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'short-deliveries', role, query],
    queryFn: () => warehouseApi.getShortDeliveries(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useShortDelivery = (id: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'short-delivery', role, id],
    queryFn: () => warehouseApi.getShortDelivery(role, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useResolveShortDelivery = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, body }: { id: number; body: ResolveShortDeliveryPayload }) =>
      warehouseApi.resolveShortDelivery(role, id, body),
    { invalidate: [WAREHOUSE_KEY], successMessage: MESSAGES.warehouse.shortageResolved },
  );
};

export const useCreateSupplementary = () => {
  const role = useRolePath();
  return useApiMutation(
    (body: SaveSupplementaryPayload) => warehouseApi.createSupplementary(role, body),
    { invalidate: [WAREHOUSE_KEY], successMessage: MESSAGES.warehouse.supplementarySaved },
  );
};
