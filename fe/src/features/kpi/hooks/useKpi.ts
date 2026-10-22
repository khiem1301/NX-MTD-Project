import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { kpiApi } from '../api/kpiApi';
import { KPI_TEXT } from '../constants';
import type { KpiRole, KpiTargetFilter, SaveKpiTargetPayload } from '../types';

export const KPI_KEY = ['kpi'] as const;
const targetsKey = (filter: KpiTargetFilter, role: KpiRole) => [...KPI_KEY, role, 'targets', filter] as const;
const targetDetailKey = (kpiTargetId: number, role: KpiRole) =>
  [...KPI_KEY, role, 'target-detail', kpiTargetId] as const;
const dailyResultsKey = (kpiTargetId: number, role: KpiRole) =>
  [...KPI_KEY, role, 'daily-results', kpiTargetId] as const;

/** UC19 / UC21 — danh sách chỉ tiêu KPI tổ may kèm thực tế vs chỉ tiêu, phân trang server-side. */
export const useKpiTargets = (filter: KpiTargetFilter, role: KpiRole = 'manager') =>
  useQuery({
    queryKey: targetsKey(filter, role),
    queryFn: () => kpiApi.getTargets(filter, role),
  });

export const useKpiTargetDetail = (kpiTargetId: number | null, role: KpiRole = 'manager') =>
  useQuery({
    queryKey: targetDetailKey(kpiTargetId ?? 0, role),
    queryFn: () => kpiApi.getTargetById(kpiTargetId!, role),
    enabled: kpiTargetId !== null,
  });

/** MI-18 "KPI theo ngày": kết quả từng ngày của một chỉ tiêu (vw_KpiDailyResult). */
export const useKpiDailyResults = (kpiTargetId: number | null, role: KpiRole = 'manager') =>
  useQuery({
    queryKey: dailyResultsKey(kpiTargetId ?? 0, role),
    queryFn: () => kpiApi.getDailyResults(kpiTargetId!, role),
    enabled: kpiTargetId !== null,
  });

// ---------- Quản đốc lập / sửa / hủy (UC19) ----------

export const useSaveKpiTarget = (kpiTargetId: number | null) =>
  useApiMutation(
    async (body: SaveKpiTargetPayload): Promise<void> => {
      if (kpiTargetId === null) await kpiApi.createTarget(body);
      else await kpiApi.updateTarget(kpiTargetId, body);
    },
    { invalidate: [KPI_KEY], successMessage: KPI_TEXT.saved },
  );

export const useCancelKpiTarget = () =>
  useApiMutation((kpiTargetId: number) => kpiApi.cancelTarget(kpiTargetId), {
    invalidate: [KPI_KEY],
    successMessage: KPI_TEXT.cancelled,
  });
