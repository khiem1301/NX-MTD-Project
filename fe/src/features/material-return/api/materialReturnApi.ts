import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  ConfirmReturnPayload,
  CreateReturnPayload,
  MaterialReturn,
  MaterialReturnListItem,
  MaterialReturnQuery,
  ReturnableLine,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;

export const LEADER_BASE = '/api/v1/team-leader/material-returns';
export const WAREHOUSE_BASE = '/api/v1/warehouse-keeper/material-returns';

export const materialReturnApi = {
  list: (base: string, q: MaterialReturnQuery) => get<Paginated<MaterialReturnListItem>>(base, q),
  detail: (base: string, id: number) => get<MaterialReturn>(`${base}/${id}`),
  returnable: () => get<ReturnableLine[]>(`${LEADER_BASE}/returnable`),
  create: (body: CreateReturnPayload) => post<MaterialReturn>(LEADER_BASE, body),
  cancel: (id: number) => post<null>(`${LEADER_BASE}/${id}/cancel`),
  confirm: (id: number, body: ConfirmReturnPayload) =>
    post<null>(`${WAREHOUSE_BASE}/${id}/confirm`, body),
  reject: (id: number, reason: string) => post<null>(`${WAREHOUSE_BASE}/${id}/reject`, { reason }),
};
