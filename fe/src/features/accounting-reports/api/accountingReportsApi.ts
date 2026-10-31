import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse } from '@/types/api';
import type {
  AccountingReportFilter,
  MealReport,
  VehicleReport,
  WorkdayOvertimeReport,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;

export const accountingReportsApi = {
  getWorkdays: (filter: AccountingReportFilter) =>
    get<WorkdayOvertimeReport>(API.accountingReports.workdays, filter),

  getMeals: (filter: AccountingReportFilter) =>
    get<MealReport>(API.accountingReports.meals, filter),

  getVehicles: (filter: AccountingReportFilter) =>
    get<VehicleReport>(API.accountingReports.vehicles, filter),

  /** Tải file Excel 3 sheet (công/tăng ca, suất ăn, sổ xe) về máy theo đúng tháng đang xem (MSG27). */
  export: async (filter: AccountingReportFilter): Promise<void> => {
    const res = await apiClient.get<Blob>(API.accountingReports.export, {
      params: filter,
      responseType: 'blob',
    });
    const url = window.URL.createObjectURL(new Blob([res.data]));
    const link = document.createElement('a');
    link.href = url;
    link.download = `BaoCaoKeToan_${filter.periodMonth}.xlsx`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },
};
