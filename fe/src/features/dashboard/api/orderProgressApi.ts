import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { LineProgress, OrderProgress } from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;

export const orderProgressApi = {
  list: (role: string, includeClosed: boolean) =>
    get<OrderProgress[]>(`/api/v1/${role}/order-progress`, { includeClosed }),
  lines: (role: string, orderId: number) =>
    get<LineProgress[]>(`/api/v1/${role}/order-progress/${orderId}/lines`),
};
