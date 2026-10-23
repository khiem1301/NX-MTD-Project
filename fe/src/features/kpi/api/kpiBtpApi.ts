import type { RolePath } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type { KpiBtpComparison, KpiBtpOption } from '../types';

const get = async <T>(url: string): Promise<T> => (await apiClient.get<ApiResponse<T>>(url)).data.data;

const base = (role: RolePath) => `/api/v1/${role}/kpi-btp`;

export const kpiBtpApi = {
  options: (role: RolePath) => get<KpiBtpOption[]>(`${base(role)}/options`),
  comparison: (role: RolePath, kpiTargetId: number) => get<KpiBtpComparison>(`${base(role)}/${kpiTargetId}`),
};
