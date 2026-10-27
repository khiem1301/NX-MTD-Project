import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  AllocationQuery,
  ConfirmAllocationPayload,
  ConfirmOnBehalfPayload,
  CreateAllocationPayload,
  WorkAllocation,
  WorkAllocationBoard,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;

export const LEADER_BASE = '/api/v1/team-leader/work-allocations';
export const WORKER_BASE = '/api/v1/worker/work-allocations';

export const pieceMovementApi = {
  list: (base: string, q: AllocationQuery) => get<Paginated<WorkAllocation>>(base, q),
  board: (orderId?: number) => get<WorkAllocationBoard>(`${LEADER_BASE}/board`, { orderId }),
  create: (body: CreateAllocationPayload) => post<WorkAllocation>(LEADER_BASE, body),
  confirmOnBehalf: (id: number, body: ConfirmOnBehalfPayload) =>
    post<null>(`${LEADER_BASE}/${id}/confirm-on-behalf`, body),
  confirmByWorker: (id: number, body: ConfirmAllocationPayload) =>
    post<null>(`${WORKER_BASE}/${id}/confirm`, body),
  rejectByWorker: (id: number, reason: string) =>
    post<null>(`${WORKER_BASE}/${id}/reject`, { reason }),
};
