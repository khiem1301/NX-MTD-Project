import { useQueries } from '@tanstack/react-query';
import { REQUESTS_KEY } from '@/hooks/useRequests';
import { requestApi } from '@/services/requestApi';
import type { RequestStatus } from '@/constants/requestTypes';
import type { RequestListQuery } from '@/types/request';
import { WAITING_STATUSES } from '../constants';

/** Số đơn từng trạng thái đang chờ (theo bộ lọc hiện tại) để hiện trên tab; chỉ lấy tổng, không lấy dòng. */
export function useRequestStatusCounts(
  scope: 'mine' | 'inbox',
  filters: Pick<RequestListQuery, 'keyword' | 'requestType'>,
): Partial<Record<RequestStatus, number>> {
  const results = useQueries({
    queries: WAITING_STATUSES.map((status) => {
      const query: RequestListQuery = { ...filters, status, page: 1, pageSize: 1 };
      return {
        queryKey: [...REQUESTS_KEY, scope, 'count', query],
        queryFn: () => (scope === 'mine' ? requestApi.mine(query) : requestApi.inbox(query)),
      };
    }),
  });
  return Object.fromEntries(
    WAITING_STATUSES.map((s, i) => [s, results[i]?.data?.totalItems]).filter(
      ([, n]) => n !== undefined,
    ),
  );
}
