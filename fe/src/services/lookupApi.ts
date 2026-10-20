import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { MaterialOption, OrderLineOption, OrderOption, TeamOption } from '@/types/lookup';

/** Dropdown dùng chung nhiều feature (GET /api/v1/common/lookups/...). */
export const lookupApi = {
  orders: async (keyword?: string): Promise<OrderOption[]> =>
    (await apiClient.get<ApiResponse<OrderOption[]>>(API.lookups.orders, { params: { keyword } }))
      .data.data,
  orderLines: async (orderId: number): Promise<OrderLineOption[]> =>
    (await apiClient.get<ApiResponse<OrderLineOption[]>>(API.lookups.orderLines(orderId))).data
      .data,
  teams: async (teamType?: string): Promise<TeamOption[]> =>
    (await apiClient.get<ApiResponse<TeamOption[]>>(API.lookups.teams, { params: { teamType } }))
      .data.data,
  materials: async (keyword?: string): Promise<MaterialOption[]> =>
    (
      await apiClient.get<ApiResponse<MaterialOption[]>>(API.lookups.materials, {
        params: { keyword },
      })
    ).data.data,
};
