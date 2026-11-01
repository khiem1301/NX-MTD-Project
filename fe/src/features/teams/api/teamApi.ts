import { API, type RolePath } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type {
  AppointPosition,
  SaveTeamPayload,
  TeamDetail,
  TeamListItem,
  TransferPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

export const teamApi = {
  list: (role: RolePath, includeDiscontinued: boolean) =>
    get<TeamListItem[]>(API.teams.base(role), { includeDiscontinued }),
  get: (role: RolePath, id: number) => get<TeamDetail>(API.teams.byId(role, id)),
  create: (role: RolePath, body: SaveTeamPayload) => post<number>(API.teams.base(role), body),
  update: (role: RolePath, id: number, body: SaveTeamPayload) =>
    put<unknown>(API.teams.byId(role, id), body),
  discontinue: (role: RolePath, id: number) =>
    post<unknown>(API.teams.action(role, id, 'discontinue')),
  reactivate: (role: RolePath, id: number) =>
    post<unknown>(API.teams.action(role, id, 'reactivate')),
  transferIn: (role: RolePath, id: number, body: TransferPayload) =>
    post<unknown>(API.teams.action(role, id, 'transfer-in'), body),
  appoint: (role: RolePath, id: number, employeeId: number, position: AppointPosition) =>
    post<unknown>(API.teams.action(role, id, 'appoint'), { employeeId, position }),
};
