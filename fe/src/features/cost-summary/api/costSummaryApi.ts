import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { CostSummary, CostSummaryFilter } from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;

export const costSummaryApi = {
  getByPeriodMonth: (filter: CostSummaryFilter) => get<CostSummary>(API.costSummary.base, filter),

  /** Tải file Excel tổng hợp chi phí về máy theo đúng tháng đang xem (MSG27). */
  export: async (filter: CostSummaryFilter): Promise<void> => {
    const res = await apiClient.get<Blob>(API.costSummary.export, {
      params: filter,
      responseType: 'blob',
    });
    const url = window.URL.createObjectURL(new Blob([res.data]));
    const link = document.createElement('a');
    link.href = url;
    link.download = `TongHopChiPhi_${filter.periodMonth}.xlsx`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },
};
