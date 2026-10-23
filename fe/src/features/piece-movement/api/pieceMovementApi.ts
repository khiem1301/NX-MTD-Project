import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  ConfirmMovementPayload,
  ConfirmOnBehalfPayload,
  CreateMovementPayload,
  MovementQuery,
  PieceMovementHoldings,
  WorkerPieceMovement,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;

export const LEADER_BASE = '/api/v1/team-leader/worker-piece-movements';
export const WORKER_BASE = '/api/v1/worker/piece-movements';

export const pieceMovementApi = {
  list: (base: string, q: MovementQuery) => get<Paginated<WorkerPieceMovement>>(base, q),
  holdings: (orderId?: number) =>
    get<PieceMovementHoldings>(`${LEADER_BASE}/holdings`, { orderId }),
  create: (body: CreateMovementPayload) => post<WorkerPieceMovement>(LEADER_BASE, body),
  confirmOnBehalf: (id: number, body: ConfirmOnBehalfPayload) =>
    post<null>(`${LEADER_BASE}/${id}/confirm-on-behalf`, body),
  confirmByWorker: (id: number, body: ConfirmMovementPayload) =>
    post<null>(`${WORKER_BASE}/${id}/confirm`, body),
  rejectByWorker: (id: number, reason: string) =>
    post<null>(`${WORKER_BASE}/${id}/reject`, { reason }),
};
