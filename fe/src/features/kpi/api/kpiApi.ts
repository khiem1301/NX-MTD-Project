import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  KpiCalendar,
  KpiDailyResult,
  KpiRole,
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

/** Quản đốc dùng API `manager/`, Admin chỉ xem qua API `admin/` (UC21). */
const baseUrl = (role: KpiRole) => (role === 'admin' ? API.kpi.adminTargets : API.kpi.targets);
const dailyUrl = (role: KpiRole, id: number) =>
  role === 'admin' ? API.kpi.adminDailyResults(id) : API.kpi.dailyResults(id);

export const kpiApi = {
  getTargets: (filter: KpiTargetFilter, role: KpiRole = 'manager') =>
    get<Paginated<KpiTarget>>(baseUrl(role), filter),

  getTargetById: (kpiTargetId: number, role: KpiRole = 'manager') =>
    get<KpiTarget>(`${baseUrl(role)}/${kpiTargetId}`),

  getDailyResults: (kpiTargetId: number, role: KpiRole = 'manager') =>
    get<KpiDailyResult[]>(dailyUrl(role, kpiTargetId)),

  /** Lịch KPI theo ngày (cả Quản đốc và Admin xem được). */
  getCalendar: (kpiTargetId: number, role: KpiRole = 'manager') => get<KpiCalendar>(`${baseUrl(role)}/${kpiTargetId}/calendar`),

  // ---------- Quản đốc lập / sửa / hủy ----------

  saveDailyPlan: (kpiTargetId: number, body: SaveKpiDailyPlanPayload) =>
    put<KpiCalendar>(`${API.kpi.targets}/${kpiTargetId}/daily-plan`, body),


  createTarget: (body: SaveKpiTargetPayload) => post<KpiTarget>(API.kpi.targets, body),

  updateTarget: (kpiTargetId: number, body: SaveKpiTargetPayload) =>
    put<KpiTarget>(`${API.kpi.targets}/${kpiTargetId}`, body),

  cancelTarget: (kpiTargetId: number) => post<void>(API.kpi.cancel(kpiTargetId)),
};
