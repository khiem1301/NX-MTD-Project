import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  MyPayslipDetail,
  MyPayslipSummary,
  PayrollDetail,
  PayrollExcelPreview,
  PayrollImportResult,
  PayrollSummary,
  RejectPayrollPayload,
  ReopenPayrollPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown, params?: object): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body, { params })).data.data;

const asFormData = (file: File): FormData => {
  const form = new FormData();
  form.append('file', file);
  return form;
};

/** GET file nhị phân (Excel) rồi cho trình duyệt tải về với tên `fileName`. */
const downloadBlob = async (url: string, fileName: string, params?: object): Promise<void> => {
  const res = await apiClient.get<Blob>(url, { params, responseType: 'blob' });
  const objectUrl = window.URL.createObjectURL(new Blob([res.data]));
  const link = document.createElement('a');
  link.href = objectUrl;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(objectUrl);
};

export type ImportedFileRole = 'accountant' | 'admin';

export const payrollApi = {
  // ---------- Kế toán (UC31) ----------

  getByPeriodMonth: (periodMonth: string) =>
    get<PayrollSummary>(API.payroll.accountant, { periodMonth }),

  /** Tải file Excel dữ liệu tính lương về máy (MSG27). */
  exportToFile: (periodMonth: string) =>
    downloadBlob(API.payroll.export, `BangLuong_${periodMonth}.xlsx`, { periodMonth }),

  /** Tải lại nguyên file Excel bảng thanh toán lương Kế toán đã nhập (Kế toán và Admin dùng chung). */
  downloadImportedFile: (payrollId: number, periodMonth: string, role: ImportedFileRole) =>
    downloadBlob(
      role === 'admin' ? API.payroll.adminFile(payrollId) : API.payroll.accountantFile(payrollId),
      `BangThanhToanLuong_${periodMonth}.xlsx`,
    ),

  previewImport: (file: File) =>
    post<PayrollExcelPreview>(API.payroll.importPreview, asFormData(file)),

  confirmImport: (file: File, periodMonth: string) =>
    post<PayrollImportResult>(API.payroll.import, asFormData(file), { periodMonth }),

  submit: (payrollId: number) => post<void>(API.payroll.submit(payrollId)),

  // ---------- Admin (UC32) ----------

  getPayrolls: (status?: string) =>
    get<Paginated<PayrollSummary>>(API.payroll.admin, { status, pageSize: 100 }),

  getDetail: (payrollId: number) => get<PayrollDetail>(API.payroll.adminDetail(payrollId)),

  approve: (payrollId: number) => post<void>(API.payroll.approve(payrollId)),

  reject: (payrollId: number, body: RejectPayrollPayload) =>
    post<void>(API.payroll.reject(payrollId), body),

  reopen: (payrollId: number, body: ReopenPayrollPayload) =>
    post<void>(API.payroll.reopen(payrollId), body),

  // ---------- Công nhân (UC06) ----------

  getMyPayslips: () => get<MyPayslipSummary[]>(API.payroll.myPayslips),

  getMyPayslip: (periodMonth: string) => get<MyPayslipDetail>(API.payroll.myPayslip(periodMonth)),
};
