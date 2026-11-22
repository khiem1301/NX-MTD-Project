import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type { VehicleLiveStatus, VehicleLiveStatusQuery } from '../types';

const VEHICLES = '/api/v1/admin/vehicles';

export const vehicleStatusApi = {
  list: async (q: VehicleLiveStatusQuery) =>
    (await apiClient.get<ApiResponse<Paginated<VehicleLiveStatus>>>(VEHICLES, { params: q })).data
      .data,
};
