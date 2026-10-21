import { useMutation, useQuery } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import type { ApiError } from '@/types/api';
import { revenueApi, type RevenueScope } from '../api/revenueApi';
import type { RevenueFilter } from '../types';

export const REVENUE_KEY = ['revenue'] as const;
const listKey = (scope: RevenueScope, filter: RevenueFilter) => [...REVENUE_KEY, scope, filter] as const;

/** UC27 — doanh thu theo tháng/đơn/mã hàng, phân trang server-side; scope quyết định route BE gọi. */
export const useRevenue = (scope: RevenueScope, filter: RevenueFilter) =>
  useQuery({
    queryKey: listKey(scope, filter),
    queryFn: () => revenueApi.getRevenue(scope, filter),
  });

export const useExportRevenue = (scope: RevenueScope) =>
  useMutation<void, ApiError, RevenueFilter>({
    mutationFn: (filter) => revenueApi.exportRevenue(scope, filter),
    onSuccess: () => void message.success(MESSAGES.revenue.exportSuccess),
    onError: () => void message.error(MESSAGES.revenue.exportFailed),
  });
