import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type { KpiDailyResult, KpiTarget, KpiTargetFilter } from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;

export const kpiApi = {
  getTargets: (filter: KpiTargetFilter) => get<Paginated<KpiTarget>>(API.kpi.targets, filter),

  getTargetById: (kpiTargetId: number) => get<KpiTarget>(`${API.kpi.targets}/${kpiTargetId}`),

  getDailyResults: (kpiTargetId: number) =>
    get<KpiDailyResult[]>(API.kpi.dailyResults(kpiTargetId)),
};
