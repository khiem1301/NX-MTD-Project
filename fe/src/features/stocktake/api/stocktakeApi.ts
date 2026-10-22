import { API, type RolePath } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type { SaveStocktakePayload, Stocktake, StocktakeListItem, StocktakeQuery } from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

export const stocktakeApi = {
  list: (role: RolePath, q: StocktakeQuery) =>
    get<Paginated<StocktakeListItem>>(API.warehouse.stocktakes(role), q),
  detail: (role: RolePath, id: number) => get<Stocktake>(`${API.warehouse.stocktakes(role)}/${id}`),
  create: (role: RolePath, body: SaveStocktakePayload) =>
    post<Stocktake>(API.warehouse.stocktakes(role), body),
  update: (role: RolePath, id: number, body: SaveStocktakePayload) =>
    put<Stocktake>(`${API.warehouse.stocktakes(role)}/${id}`, body),
  approve: (role: RolePath, id: number) =>
    post<null>(`${API.warehouse.stocktakes(role)}/${id}/approve`),
  returnToKeeper: (role: RolePath, id: number, reason: string) =>
    post<null>(`${API.warehouse.stocktakes(role)}/${id}/return`, { reason }),
};
