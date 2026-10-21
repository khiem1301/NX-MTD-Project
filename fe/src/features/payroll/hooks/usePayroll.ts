import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import type { ApiError } from '@/types/api';
import { payrollApi, type ImportedFileRole } from '../api/payrollApi';
import type {
  PayrollExcelPreview,
  PayrollImportResult,
  RejectPayrollPayload,
  ReopenPayrollPayload,
} from '../types';

export const PAYROLL_KEY = ['payroll'] as const;
const byMonthKey = (periodMonth: string) => [...PAYROLL_KEY, 'by-month', periodMonth] as const;
const adminListKey = (status?: string) => [...PAYROLL_KEY, 'admin-list', status ?? 'all'] as const;
const adminDetailKey = (payrollId: number) => [...PAYROLL_KEY, 'admin-detail', payrollId] as const;
const myPayslipsKey = [...PAYROLL_KEY, 'my-payslips'] as const;
const myPayslipKey = (periodMonth: string) => [...PAYROLL_KEY, 'my-payslip', periodMonth] as const;

// ---------- Kế toán (UC31) ----------

/** Bảng lương hiện tại của một tháng (404 nếu chưa nhập lần nào — không coi là lỗi để hiện UI). */
export const usePayrollByMonth = (periodMonth: string) =>
  useQuery({
    queryKey: byMonthKey(periodMonth),
    queryFn: () => payrollApi.getByPeriodMonth(periodMonth),
    retry: false,
    throwOnError: false,
  });

export const useExportPayroll = () =>
  useMutation<void, ApiError, string>({
    mutationFn: (periodMonth) => payrollApi.exportToFile(periodMonth),
    onSuccess: () => void message.success(MESSAGES.payroll.exported),
    onError: () => void message.error(MESSAGES.payroll.exportFailed),
  });

/** Tải file gốc Kế toán đã nhập (nút có ở trang Kế toán và trang duyệt của Admin). */
export const useDownloadImportedFile = (role: ImportedFileRole) =>
  useMutation<void, ApiError, { payrollId: number; periodMonth: string }>({
    mutationFn: ({ payrollId, periodMonth }) =>
      payrollApi.downloadImportedFile(payrollId, periodMonth, role),
    onError: () => void message.error(MESSAGES.payroll.downloadFileFailed),
  });

export const usePreviewImport = () =>
  useMutation<PayrollExcelPreview, ApiError, File>({
    mutationFn: (file) => payrollApi.previewImport(file),
    onError: (error) => void message.error(error.message),
  });

export const useConfirmImport = (periodMonth: string) => {
  const queryClient = useQueryClient();
  return useMutation<PayrollImportResult, ApiError, File>({
    mutationFn: (file) => payrollApi.confirmImport(file, periodMonth),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: byMonthKey(periodMonth) });
      void message.success(
        `${MESSAGES.payroll.importedPrefix} ${result.validCount} dòng hợp lệ, ${result.errorCount} dòng lỗi, ${result.duplicateCount} dòng trùng.`,
      );
    },
    onError: (error) => void message.error(error.message),
  });
};

export const useSubmitPayroll = (periodMonth: string) =>
  useApiMutation((payrollId: number) => payrollApi.submit(payrollId), {
    invalidate: [byMonthKey(periodMonth)],
    successMessage: MESSAGES.payroll.submitted,
  });

// ---------- Admin (UC32) ----------

export const useAdminPayrolls = (status?: string) =>
  useQuery({
    queryKey: adminListKey(status),
    queryFn: () => payrollApi.getPayrolls(status),
  });

export const usePayrollDetail = (payrollId: number | null) =>
  useQuery({
    queryKey: adminDetailKey(payrollId ?? 0),
    queryFn: () => payrollApi.getDetail(payrollId!),
    enabled: payrollId !== null,
  });

const invalidateAdminAndDetail = (payrollId: number) => [
  [...PAYROLL_KEY, 'admin-list'],
  adminDetailKey(payrollId),
];

export const useApprovePayroll = (payrollId: number) =>
  useApiMutation(() => payrollApi.approve(payrollId), {
    invalidate: invalidateAdminAndDetail(payrollId),
    successMessage: MESSAGES.payroll.approved,
  });

export const useRejectPayroll = (payrollId: number) =>
  useApiMutation((body: RejectPayrollPayload) => payrollApi.reject(payrollId, body), {
    invalidate: invalidateAdminAndDetail(payrollId),
    successMessage: MESSAGES.payroll.rejected,
  });

export const useReopenPayroll = (payrollId: number) =>
  useApiMutation((body: ReopenPayrollPayload) => payrollApi.reopen(payrollId, body), {
    invalidate: invalidateAdminAndDetail(payrollId),
    successMessage: MESSAGES.payroll.reopened,
  });

// ---------- Công nhân (UC06) ----------

export const useMyPayslips = () =>
  useQuery({ queryKey: myPayslipsKey, queryFn: payrollApi.getMyPayslips });

export const useMyPayslip = (periodMonth: string | null) =>
  useQuery({
    queryKey: myPayslipKey(periodMonth ?? ''),
    queryFn: () => payrollApi.getMyPayslip(periodMonth!),
    enabled: periodMonth !== null,
  });
