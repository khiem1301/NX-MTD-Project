import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type { OrderLineLookup, OrderLookup, ProductLookup } from '@/types/orderLookup';

type LookupParams = { keyword?: string; page?: number; pageSize?: number; status?: string };

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;

/** GET /api/v1/common/order-lookups/... (mọi vai trò trừ Công nhân). */
export const orderLookupApi = {
  orders: (params?: LookupParams) => get<Paginated<OrderLookup>>(API.orderLookups.orders, params),
  orderLines: (orderId: number) => get<OrderLineLookup[]>(API.orderLookups.orderLines(orderId)),
  products: (params?: LookupParams) => get<Paginated<ProductLookup>>(API.orderLookups.products, params),
  productColors: () => get<string[]>(API.orderLookups.productColors),
};
