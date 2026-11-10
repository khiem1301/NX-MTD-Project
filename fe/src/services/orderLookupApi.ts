import { API, type ProductScope } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  OrderLineLookup,
  OrderLookup,
  ProductComponentLookup,
  ProductLookup,
} from '@/types/orderLookup';

type LookupParams = { keyword?: string; page?: number; pageSize?: number; status?: string };

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;

/** GET /api/v1/common/order-lookups/... (mọi vai trò trừ Công nhân). */
export const orderLookupApi = {
  orders: (params?: LookupParams) => get<Paginated<OrderLookup>>(API.orderLookups.orders, params),
  /** sewingTeamId: Quản đốc / Admin chỉ lấy dòng chia cho tổ đang chọn. */
  orderLines: (orderId: number, sewingTeamId?: number) =>
    get<OrderLineLookup[]>(API.orderLookups.orderLines(orderId), { sewingTeamId }),
  products: (params?: LookupParams) =>
    get<Paginated<ProductLookup>>(API.orderLookups.products, params),
  productColors: () => get<string[]>(API.orderLookups.productColors),
  /** Chi tiết của mã hàng, đọc từ API định mức (Admin / Kế toán). */
  productComponents: async (productId: number, scope: ProductScope) =>
    (await get<{ components: ProductComponentLookup[] }>(API.products.spec(productId, scope)))
      .components,
};
