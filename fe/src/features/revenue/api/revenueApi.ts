import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type { RevenueFilter, RevenueSummary } from '../types';

/** Doanh thu dùng chung được cả Kế toán và Admin xem (GBR-02) — route khác nhau theo tiền tố role,
 * contract (DTO, filter) giống hệt nhau. */
export type RevenueScope = 'accountant' | 'admin';

const endpointsOf = (scope: RevenueScope) =>
  scope === 'admin'
    ? { list: API.revenue.admin, export: API.revenue.adminExport }
    : { list: API.revenue.accountant, export: API.revenue.accountantExport };

export const revenueApi = {
  getRevenue: async (scope: RevenueScope, filter: RevenueFilter): Promise<Paginated<RevenueSummary>> =>
    (await apiClient.get<ApiResponse<Paginated<RevenueSummary>>>(endpointsOf(scope).list, { params: filter })).data
      .data,

  exportRevenue: async (scope: RevenueScope, filter: RevenueFilter): Promise<void> => {
    const res = await apiClient.get<Blob>(endpointsOf(scope).export, { params: filter, responseType: 'blob' });
    const url = window.URL.createObjectURL(new Blob([res.data]));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'DoanhThu.xlsx';
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },
};
