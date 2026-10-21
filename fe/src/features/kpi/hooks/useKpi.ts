import { useQuery } from '@tanstack/react-query';
import { kpiApi } from '../api/kpiApi';
import type { KpiTargetFilter } from '../types';

export const KPI_KEY = ['kpi'] as const;
const targetsKey = (filter: KpiTargetFilter) => [...KPI_KEY, 'targets', filter] as const;
const targetDetailKey = (kpiTargetId: number) => [...KPI_KEY, 'target-detail', kpiTargetId] as const;
const dailyResultsKey = (kpiTargetId: number) => [...KPI_KEY, 'daily-results', kpiTargetId] as const;

/** UC19 — Quản đốc: danh sách chỉ tiêu KPI tổ may, phân trang server-side. */
export const useKpiTargets = (filter: KpiTargetFilter) =>
  useQuery({
    queryKey: targetsKey(filter),
    queryFn: () => kpiApi.getTargets(filter),
  });

export const useKpiTargetDetail = (kpiTargetId: number | null) =>
  useQuery({
    queryKey: targetDetailKey(kpiTargetId ?? 0),
    queryFn: () => kpiApi.getTargetById(kpiTargetId!),
    enabled: kpiTargetId !== null,
  });

/** MI-18 "KPI theo ngày": kết quả từng ngày của một chỉ tiêu (vw_KpiDailyResult). */
export const useKpiDailyResults = (kpiTargetId: number | null) =>
  useQuery({
    queryKey: dailyResultsKey(kpiTargetId ?? 0),
    queryFn: () => kpiApi.getDailyResults(kpiTargetId!),
    enabled: kpiTargetId !== null,
  });
