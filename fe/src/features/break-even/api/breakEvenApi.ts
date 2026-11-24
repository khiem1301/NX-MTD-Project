import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type {
  BreakEvenCalculationResult,
  BreakEvenFilter,
  BreakEvenInputs,
  BreakEvenListResult,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;

export const breakEvenApi = {
  getBreakEven: (filter: BreakEvenFilter) => get<BreakEvenListResult>(API.breakEven.base, filter),

  /** Dữ liệu đầu vào của tháng (bảng lương, chi phí, thành phẩm, giờ công) — DEV-ALL-171. */
  inputs: (periodMonth: string) => get<BreakEvenInputs>(API.breakEven.inputs, { periodMonth }),

  /** Một lần tính chạy cả hai cách chia định phí (DEV-ALL-171). */
  calculate: (periodMonth: string) =>
    post<BreakEvenCalculationResult>(API.breakEven.calculate, { periodMonth }),

  /** Tải file Excel hòa vốn về máy theo đúng bộ lọc đang xem (MSG27). */
  export: async (filter: BreakEvenFilter): Promise<void> => {
    const res = await apiClient.get<Blob>(API.breakEven.export, {
      params: filter,
      responseType: 'blob',
    });
    const url = window.URL.createObjectURL(new Blob([res.data]));
    const link = document.createElement('a');
    link.href = url;
    link.download = `HoaVon_${filter.periodMonth}.xlsx`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },
};
