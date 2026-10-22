import { useMutation, useQuery } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { costSummaryApi } from '../api/costSummaryApi';
import type { CostSummaryFilter } from '../types';

export const COST_SUMMARY_KEY = ['cost-summary'] as const;
const byMonthKey = (filter: CostSummaryFilter) => [...COST_SUMMARY_KEY, filter] as const;

/** UC12 — tổng hợp chi phí của một tháng; 404 (chưa nhập chi phí tháng) không coi là lỗi để hiện UI. */
export const useCostSummary = (filter: CostSummaryFilter) =>
  useQuery({
    queryKey: byMonthKey(filter),
    queryFn: () => costSummaryApi.getByPeriodMonth(filter),
    retry: false,
    throwOnError: false,
  });

export const useExportCostSummary = () =>
  useMutation<void, ApiError, CostSummaryFilter>({
    mutationFn: (filter) => costSummaryApi.export(filter),
    onSuccess: () => void message.success(MESSAGES.costSummary.exportSuccess),
    onError: () => void message.error(MESSAGES.costSummary.exportFailed),
  });
