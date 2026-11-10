import { API, type RolePath } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type { StockBalance, StockBalanceQuery } from '@/types/stock';

/** Tồn kho dùng chung cho kho và kiểm kê (GET /api/v1/<role>/inventories). */
export const stockApi = {
  getBalances: async (
    role: RolePath,
    params: StockBalanceQuery,
  ): Promise<Paginated<StockBalance>> =>
    (
      await apiClient.get<ApiResponse<Paginated<StockBalance>>>(API.warehouse.inventories(role), {
        params,
      })
    ).data.data,
};
