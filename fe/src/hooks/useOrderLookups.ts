import { useQuery } from '@tanstack/react-query';
import { orderLookupApi } from '@/services/orderLookupApi';

const KEY = ['order-lookups'] as const;

/** Đơn đang chạy (mặc định BE: Mới, Đang sản xuất, Đang giao, Giao một phần). */
export const useOrderLookup = (keyword?: string, status?: string) =>
  useQuery({
    queryKey: [...KEY, 'orders', keyword, status],
    queryFn: () => orderLookupApi.orders({ keyword, status, pageSize: 50 }),
  });

export const useOrderLineLookup = (orderId: number | null) =>
  useQuery({
    queryKey: [...KEY, 'order-lines', orderId],
    queryFn: () => orderLookupApi.orderLines(orderId as number),
    enabled: orderId !== null,
  });

/** Màu đã khai ở các mã hàng (gợi ý khi nhập màu). */
export const useProductColors = () =>
  useQuery({ queryKey: [...KEY, 'product-colors'], queryFn: () => orderLookupApi.productColors(), staleTime: 60_000 });

/** Mã hàng đang dùng (ACTIVE). */
export const useProductLookup = (keyword?: string) =>
  useQuery({
    queryKey: [...KEY, 'products', keyword],
    queryFn: () => orderLookupApi.products({ keyword, pageSize: 50 }),
  });
