import { useQuery } from '@tanstack/react-query';
import { useRolePath } from '@/hooks/useRolePath';
import { orderProgressApi } from '../api/orderProgressApi';

/** Tiến độ từng dòng (màu - size) của một đơn; chỉ gọi khi đã chọn đơn. */
export const useOrderLines = (orderId: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: ['dashboard', 'lines', role, orderId],
    queryFn: () => orderProgressApi.lines(role, orderId ?? 0),
    enabled: orderId !== undefined,
  });
};
