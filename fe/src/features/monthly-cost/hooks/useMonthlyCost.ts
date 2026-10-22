import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { monthlyCostApi } from '../api/monthlyCostApi';
import type { MonthlyCostExcelPreview, MonthlyCostImportResult } from '../types';

export const MONTHLY_COST_KEY = ['monthly-cost'] as const;
const byMonthKey = (periodMonth: string) => [...MONTHLY_COST_KEY, 'by-month', periodMonth] as const;

/** Chi phí đã nhập của một tháng (404 nếu chưa nhập lần nào — không coi là lỗi để hiện UI). */
export const useMonthlyCostByMonth = (periodMonth: string) =>
  useQuery({
    queryKey: byMonthKey(periodMonth),
    queryFn: () => monthlyCostApi.getByPeriodMonth(periodMonth),
    retry: false,
    throwOnError: false,
  });

export const useDownloadMonthlyCostTemplate = () =>
  useMutation<void, ApiError, void>({
    mutationFn: () => monthlyCostApi.downloadTemplate(),
    onSuccess: () => void message.success(MESSAGES.monthlyCost.templateDownloaded),
    onError: () => void message.error(MESSAGES.monthlyCost.templateDownloadFailed),
  });

export const usePreviewMonthlyCostImport = (periodMonth: string) =>
  useMutation<MonthlyCostExcelPreview, ApiError, File>({
    mutationFn: (file) => monthlyCostApi.previewImport(file, periodMonth),
    onError: (error) => void message.error(error.message),
  });

export const useConfirmMonthlyCostImport = (periodMonth: string) => {
  const queryClient = useQueryClient();
  return useMutation<MonthlyCostImportResult, ApiError, File>({
    mutationFn: (file) => monthlyCostApi.confirmImport(file, periodMonth),
    onSuccess: async (result) => {
      await queryClient.invalidateQueries({ queryKey: byMonthKey(periodMonth) });
      void message.success(
        `${MESSAGES.monthlyCost.importedPrefix} ${result.validCount} dòng hợp lệ, ${result.errorCount} dòng lỗi, ${result.duplicateCount} dòng trùng.`,
      );
    },
    onError: (error) => void message.error(error.message),
  });
};
