import { useQuery } from '@tanstack/react-query';
import { lookupApi } from '@/services/lookupApi';

const LOOKUP_STALE_MS = 60_000;

export const useOrderOptions = () =>
  useQuery({
    queryKey: ['lookups', 'orders'],
    queryFn: () => lookupApi.orders(),
    staleTime: LOOKUP_STALE_MS,
  });

export const useOrderLineOptions = (orderId: number | undefined) =>
  useQuery({
    queryKey: ['lookups', 'order-lines', orderId],
    queryFn: () => lookupApi.orderLines(orderId ?? 0),
    enabled: orderId !== undefined,
    staleTime: LOOKUP_STALE_MS,
  });

export const useOrderLineComponents = (orderLineId: number | undefined) =>
  useQuery({
    queryKey: ['lookups', 'order-line-components', orderLineId],
    queryFn: () => lookupApi.orderLineComponents(orderLineId ?? 0),
    enabled: orderLineId !== undefined,
    staleTime: LOOKUP_STALE_MS,
  });

export const useTeamOptions = (teamType: string) =>
  useQuery({
    queryKey: ['lookups', 'teams', teamType],
    queryFn: () => lookupApi.teams(teamType),
    staleTime: LOOKUP_STALE_MS,
  });

export const useMaterialOptions = () =>
  useQuery({
    queryKey: ['lookups', 'materials'],
    queryFn: () => lookupApi.materials(),
    staleTime: LOOKUP_STALE_MS,
  });
