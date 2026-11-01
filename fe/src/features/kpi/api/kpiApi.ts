import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  EndOfOrderPreview,
  KpiCalendar,
  KpiDailyResult,
  KpiOrderCapacity,
  KpiTarget,
  KpiTargetFilter,
  SaveKpiDailyPlanPayload,
  SaveKpiTargetPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

export const kpiApi = {
  getTargets: (filter: KpiTargetFilter) => get<Paginated<KpiTarget>>(API.kpi.targets, filter),

  getTargetById: (kpiTargetId: number) => get<KpiTarget>(`${API.kpi.targets}/${kpiTargetId}`),

  getDailyResults: (kpiTargetId: number) =>
    get<KpiDailyResult[]>(API.kpi.dailyResults(kpiTargetId)),

  getCalendar: (kpiTargetId: number) => get<KpiCalendar>(API.kpi.calendar(kpiTargetId)),

  // ---------- Quản đốc, Admin lập / sửa / hủy ----------

  getOrderCapacity: (params: { orderId: number; productId: number; exceptKpiTargetId?: number }) =>
    get<KpiOrderCapacity>(API.kpi.orderCapacity, params),

  saveDailyPlan: (kpiTargetId: number, body: SaveKpiDailyPlanPayload) =>
    put<KpiCalendar>(API.kpi.dailyPlan(kpiTargetId), body),

  finishPreview: (kpiTargetId: number) =>
    get<EndOfOrderPreview>(API.kpi.finishPreview(kpiTargetId)),

  finish: (kpiTargetId: number) => post<EndOfOrderPreview>(API.kpi.finish(kpiTargetId)),

  createTarget: (body: SaveKpiTargetPayload) => post<KpiTarget>(API.kpi.targets, body),

  updateTarget: (kpiTargetId: number, body: SaveKpiTargetPayload) =>
    put<KpiTarget>(`${API.kpi.targets}/${kpiTargetId}`, body),

  cancelTarget: (kpiTargetId: number) => post<void>(API.kpi.cancel(kpiTargetId)),
};
