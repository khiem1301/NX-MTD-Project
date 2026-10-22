import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  ConfirmDeliveryPayload,
  CreateDispatchPayload,
  DispatchableLine,
  DispatchNote,
  DispatchNoteListItem,
  DispatchNoteQuery,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;

export const MANAGER_BASE = '/api/v1/manager/dispatch-notes';
export const WAREHOUSE_BASE = '/api/v1/warehouse-keeper/dispatch-notes';
export const ACCOUNTANT_BASE = '/api/v1/accountant/dispatch-notes';

/** Ba vai trò dùng ba nhóm URL; base do hook chọn theo vai trò đăng nhập. */
export const dispatchApi = {
  list: (base: string, q: DispatchNoteQuery) => get<Paginated<DispatchNoteListItem>>(base, q),
  detail: (base: string, id: number) => get<DispatchNote>(`${base}/${id}`),
  dispatchable: (orderId: number) =>
    get<DispatchableLine[]>(`${MANAGER_BASE}/dispatchable`, { orderId }),
  create: (body: CreateDispatchPayload) => post<DispatchNote>(MANAGER_BASE, body),
  cancel: (id: number) => post<null>(`${MANAGER_BASE}/${id}/cancel`),
  exportGoods: (id: number) => post<null>(`${WAREHOUSE_BASE}/${id}/export`),
  reject: (id: number, reason: string) => post<null>(`${WAREHOUSE_BASE}/${id}/reject`, { reason }),
  confirmDelivery: (id: number, body: ConfirmDeliveryPayload) =>
    post<null>(`${ACCOUNTANT_BASE}/${id}/confirm-delivery`, body),
};
