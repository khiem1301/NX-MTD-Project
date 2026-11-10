import { useQuery } from '@tanstack/react-query';
import { useProductScope } from '@/hooks/useProductScope';
import { orderLookupApi } from '@/services/orderLookupApi';

const KEY = ['order-lookups'] as const;

/** Đơn đang chạy (mặc định BE: Mới, Đang sản xuất, Đang giao, Giao một phần). */
export const useOrderLookup = (keyword?: string, status?: string) =>
  useQuery({
    queryKey: [...KEY, 'orders', keyword, status],
    queryFn: () => orderLookupApi.orders({ keyword, status, pageSize: 50 }),
  });

/** sewingTeamId: Quản đốc / Admin chỉ lấy dòng chia cho tổ đang chọn (tổ may tự lọc theo tổ mình ở BE). */
export const useOrderLineLookup = (orderId: number | null, sewingTeamId?: number) =>
  useQuery({
    queryKey: [...KEY, 'order-lines', orderId, sewingTeamId],
    queryFn: () => orderLookupApi.orderLines(orderId as number, sewingTeamId),
    enabled: orderId !== null,
  });

/** Màu đã khai ở các mã hàng (gợi ý khi nhập màu). */
export const useProductColors = () =>
  useQuery({
    queryKey: [...KEY, 'product-colors'],
    queryFn: () => orderLookupApi.productColors(),
    staleTime: 60_000,
  });

/** Mã hàng đang dùng (ACTIVE). */
export const useProductLookup = (keyword?: string) =>
  useQuery({
    queryKey: [...KEY, 'products', keyword],
    queryFn: () => orderLookupApi.products({ keyword, pageSize: 50 }),
  });

/** Chi tiết của mã hàng. Nằm dưới key 'orders' như định mức, để lưu định mức xong tự làm mới. */
export const useProductComponents = (productId: number | undefined) => {
  const scope = useProductScope();
  return useQuery({
    queryKey: ['orders', 'products', 'spec-components', scope, productId],
    queryFn: () => orderLookupApi.productComponents(productId ?? 0, scope),
    enabled: productId !== undefined,
  });
};
