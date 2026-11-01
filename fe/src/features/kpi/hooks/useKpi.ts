import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { kpiApi } from '../api/kpiApi';
import { KPI_TEXT } from '../constants';
import type {
  KpiRole,
  KpiTargetFilter,
  SaveKpiDailyPlanPayload,
  SaveKpiTargetPayload,
} from '../types';

export const KPI_KEY = ['kpi'] as const;
const targetsKey = (filter: KpiTargetFilter, role: KpiRole) =>
  [...KPI_KEY, role, 'targets', filter] as const;
const targetDetailKey = (kpiTargetId: number, role: KpiRole) =>
  [...KPI_KEY, role, 'target-detail', kpiTargetId] as const;

/** UC19 / UC21 — danh sách chỉ tiêu KPI tổ may kèm thực tế vs chỉ tiêu, phân trang server-side. */
export const useKpiTargets = (filter: KpiTargetFilter, role: KpiRole = 'manager') =>
  useQuery({
    queryKey: targetsKey(filter, role),
    queryFn: () => kpiApi.getTargets(filter, role),
  });

/** Chỉ tiêu tối đa lập được theo số lượng đơn đặt; chỉ gọi khi đã chọn đơn và mã hàng. */
export const useKpiOrderCapacity = (
  orderId: number | null,
  productId: number | null,
  exceptKpiTargetId?: number,
) =>
  useQuery({
    queryKey: [...KPI_KEY, 'order-capacity', orderId, productId, exceptKpiTargetId],
    queryFn: () =>
      kpiApi.getOrderCapacity({ orderId: orderId!, productId: productId!, exceptKpiTargetId }),
    enabled: orderId !== null && productId !== null,
  });

/** Lịch KPI theo ngày của một chỉ tiêu, kèm BTP khả dụng. */
export const useKpiCalendar = (kpiTargetId: number | null, role: KpiRole) =>
  useQuery({
    queryKey: [...KPI_KEY, role, 'calendar', kpiTargetId],
    queryFn: () => kpiApi.getCalendar(kpiTargetId!, role),
    enabled: kpiTargetId !== null,
  });

export const useKpiTargetDetail = (kpiTargetId: number | null, role: KpiRole = 'manager') =>
  useQuery({
    queryKey: targetDetailKey(kpiTargetId ?? 0, role),
    queryFn: () => kpiApi.getTargetById(kpiTargetId!, role),
    enabled: kpiTargetId !== null,
  });

/** Xem trước kết thúc đơn của tổ: lệch kiểm cuối đơn theo công nhân, kết quả KPI, tăng ca. */
export const useFinishPreview = (kpiTargetId: number | null) =>
  useQuery({
    queryKey: [...KPI_KEY, 'finish-preview', kpiTargetId],
    queryFn: () => kpiApi.finishPreview(kpiTargetId!),
    enabled: kpiTargetId !== null,
  });

/** Kết thúc đơn: ghi phiếu hàng hỏng cho phần lệch, chốt và đóng KPI; làm mới cả hàng hỏng và đơn từ (đơn tăng ca). */
export const useFinishKpiTarget = () =>
  useApiMutation((kpiTargetId: number) => kpiApi.finish(kpiTargetId), {
    invalidate: [KPI_KEY, ['production'], ['requests']],
    successMessage: KPI_TEXT.finish.done,
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

export const useSaveKpiDailyPlan = (kpiTargetId: number) =>
  useApiMutation((body: SaveKpiDailyPlanPayload) => kpiApi.saveDailyPlan(kpiTargetId, body), {
    invalidate: [KPI_KEY],
    successMessage: KPI_TEXT.calendar.saved,
  });

export const useCancelKpiTarget = () =>
  useApiMutation((kpiTargetId: number) => kpiApi.cancelTarget(kpiTargetId), {
    invalidate: [KPI_KEY],
    successMessage: KPI_TEXT.cancelled,
  });
