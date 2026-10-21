import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  CreateRequestBody,
  RequestAction,
  RequestDetail,
  RequestListItem,
  RequestListQuery,
  RequestTypeOption,
  UpdateRequestBody,
} from '../types';

const data = <T>(res: { data: ApiResponse<T> }): T => res.data.data;

export const requestApi = {
  types: async (): Promise<RequestTypeOption[]> =>
    data(await apiClient.get<ApiResponse<RequestTypeOption[]>>(API.requests.types)),
  mine: async (params: RequestListQuery): Promise<Paginated<RequestListItem>> =>
    data(await apiClient.get<ApiResponse<Paginated<RequestListItem>>>(API.requests.base, { params })),
  inbox: async (params: RequestListQuery): Promise<Paginated<RequestListItem>> =>
    data(await apiClient.get<ApiResponse<Paginated<RequestListItem>>>(API.requests.inbox, { params })),
  detail: async (id: number): Promise<RequestDetail> =>
    data(await apiClient.get<ApiResponse<RequestDetail>>(API.requests.byId(id))),
  /** BE trả Id đơn mới. */
  create: async (body: CreateRequestBody): Promise<number> =>
    data(await apiClient.post<ApiResponse<number>>(API.requests.base, body)),
  update: async (id: number, body: UpdateRequestBody): Promise<void> => {
    await apiClient.put(API.requests.byId(id), body);
  },
  act: async (action: RequestAction): Promise<void> => {
    const { kind, id, ...body } = action;
    const url = kind === 'cancel' ? API.requests.cancel(id) : API.requests[kind](id);
    await apiClient.post(url, body);
  },
};
