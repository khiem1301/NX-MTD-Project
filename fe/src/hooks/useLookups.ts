import dayjs, { type Dayjs } from 'dayjs';
import { useQuery } from '@tanstack/react-query';
import { lookupApi } from '@/services/lookupApi';

const LOOKUP_STALE_MS = 60_000;

export const useOrderOptions = (sewingTeamId?: number) =>
  useQuery({
    queryKey: ['lookups', 'orders', sewingTeamId],
    queryFn: () => lookupApi.orders(undefined, sewingTeamId),
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

export const useScrapMaterialOptions = (enabled = true) =>
  useQuery({
    queryKey: ['lookups', 'scrap-materials'],
    queryFn: () => lookupApi.scrapMaterials(),
    staleTime: LOOKUP_STALE_MS,
    enabled,
  });

export const useMaterialOptions = () =>
  useQuery({
    queryKey: ['lookups', 'materials'],
    queryFn: () => lookupApi.materials(),
    staleTime: LOOKUP_STALE_MS,
  });

/**
 * Ngày làm việc chọn được: từ ngày nhận đơn đến hôm nay (thời gian gia công). Chưa chọn đơn thì lấy ngày nhận đơn sớm nhất trong
 * các đơn đang mở, nên không chọn được ngày trước mọi đơn. Khớp kiểm tra ở BE (ProductionGuard.BeforeOrderStart).
 */
export const useWorkDateRange = (orderId: number | undefined) => {
  const orders = useOrderOptions();
  const options = orders.data ?? [];
  const starts =
    orderId === undefined
      ? options.map((o) => o.receivedDate)
      : options.filter((o) => o.orderId === orderId).map((o) => o.receivedDate);
  const start = starts.length === 0 ? undefined : starts.reduce((min, d) => (d < min ? d : min));
  return (date: Dayjs) =>
    date.isAfter(dayjs(), 'day') || (start !== undefined && date.isBefore(dayjs(start), 'day'));
};
