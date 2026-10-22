import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { MonthlyCostExcelPreview, MonthlyCostImportResult, MonthlyCostSummary } from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown, params?: object): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body, { params })).data.data;

const asFormData = (file: File): FormData => {
  const form = new FormData();
  form.append('file', file);
  return form;
};

export const monthlyCostApi = {
  getByPeriodMonth: (periodMonth: string) =>
    get<MonthlyCostSummary>(API.monthlyCost.base, { periodMonth }),

  /** Tải file Excel mẫu về máy (MSG27). */
  downloadTemplate: async (): Promise<void> => {
    const res = await apiClient.get<Blob>(API.monthlyCost.template, { responseType: 'blob' });
    const url = window.URL.createObjectURL(new Blob([res.data]));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'MauChiPhiThang.xlsx';
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },

  previewImport: (file: File, periodMonth: string) =>
    post<MonthlyCostExcelPreview>(API.monthlyCost.importPreview, asFormData(file), { periodMonth }),

  confirmImport: (file: File, periodMonth: string) =>
    post<MonthlyCostImportResult>(API.monthlyCost.import, asFormData(file), { periodMonth }),
};
