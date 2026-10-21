import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { breakEvenApi } from '../api/breakEvenApi';
import type { AllocationBasis, BreakEvenCalculationResult, BreakEvenFilter } from '../types';

export const BREAK_EVEN_KEY = ['break-even'] as const;
const listKey = (filter: BreakEvenFilter) => [...BREAK_EVEN_KEY, filter] as const;

/** UC11 — kết quả hòa vốn đã tính của một tháng; 404 (chưa tính lần nào) không coi là lỗi để hiện UI. */
export const useBreakEven = (filter: BreakEvenFilter) =>
  useQuery({
    queryKey: listKey(filter),
    queryFn: () => breakEvenApi.getBreakEven(filter),
    retry: false,
    throwOnError: false,
  });

export const useCalculateBreakEven = (periodMonth: string) => {
  const queryClient = useQueryClient();
  return useMutation<BreakEvenCalculationResult, ApiError, AllocationBasis>({
    mutationFn: (allocationBasis) => breakEvenApi.calculate(periodMonth, allocationBasis),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: BREAK_EVEN_KEY });
      void message.success(MESSAGES.breakEven.calculated);
    },
    onError: (error) => void message.error(error.message),
  });
};

export const useExportBreakEven = () =>
  useMutation<void, ApiError, BreakEvenFilter>({
    mutationFn: (filter) => breakEvenApi.export(filter),
    onSuccess: () => void message.success(MESSAGES.breakEven.exportSuccess),
    onError: () => void message.error(MESSAGES.breakEven.exportFailed),
  });
