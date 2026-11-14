import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { kpiApi } from '../api/kpiApi';
import { KPI_TEXT } from '../constants';
import type { KpiTargetFilter, SaveKpiDailyPlanPayload, SaveKpiTargetPayload } from '../types';

export const KPI_KEY = ['kpi'] as const;
const targetsKey = (filter: KpiTargetFilter) => [...KPI_KEY, 'targets', filter] as const;
const targetDetailKey = (kpiTargetId: number) =>
  [...KPI_KEY, 'target-detail', kpiTargetId] as const;

/** UC19 / UC21 — danh sách chỉ tiêu KPI tổ may kèm thực tế vs chỉ tiêu, phân trang server-side. */
export const useKpiTargets = (filter: KpiTargetFilter) =>
  useQuery({
    queryKey: targetsKey(filter),
    queryFn: () => kpiApi.getTargets(filter),
  });

/** Tổ trưởng / tổ phó may xem chỉ tiêu KPI được giao cho tổ của mình. */
export const useTeamKpiTargets = (filter: KpiTargetFilter) =>
  useQuery({
    queryKey: [...KPI_KEY, 'team-targets', filter],
    queryFn: () => kpiApi.getTeamTargets(filter),
  });

/** Chỉ tiêu tối đa lập được theo số lượng đơn đặt; chỉ gọi khi đã chọn đơn và mã hàng. */
/** Tổ may được chia mã hàng của đơn: chỉ các tổ này mới đặt được chỉ tiêu (DEV-ALL-49). */
export const useAllottedTeams = (orderId: number | null, productId: number | null) =>
  useQuery({
    queryKey: [...KPI_KEY, 'allotted-teams', orderId, productId],
    queryFn: () => kpiApi.getAllottedTeams(orderId as number, productId as number),
    enabled: orderId !== null && productId !== null,
  });

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

/** Các màu × size của mã hàng đã chia cho tổ và số đặt được của từng dòng; chỉ gọi khi đã chọn đơn, mã hàng và tổ. */
export const useKpiLineCapacity = (
  orderId: number | null,
  productId: number | null,
  sewingTeamId: number | null,
  exceptKpiTargetId?: number,
) =>
  useQuery({
    queryKey: [...KPI_KEY, 'line-capacity', orderId, productId, sewingTeamId, exceptKpiTargetId],
    queryFn: () =>
      kpiApi.getLineCapacity({
        orderId: orderId as number,
        productId: productId as number,
        sewingTeamId: sewingTeamId as number,
        exceptKpiTargetId,
      }),
    enabled: orderId !== null && productId !== null && sewingTeamId !== null,
  });

/** Lịch KPI theo ngày của một chỉ tiêu, kèm BTP khả dụng. */
export const useKpiCalendar = (kpiTargetId: number | null) =>
  useQuery({
    queryKey: [...KPI_KEY, 'calendar', kpiTargetId],
    queryFn: () => kpiApi.getCalendar(kpiTargetId!),
    enabled: kpiTargetId !== null,
  });

export const useKpiTargetDetail = (kpiTargetId: number | null) =>
  useQuery({
    queryKey: targetDetailKey(kpiTargetId ?? 0),
    queryFn: () => kpiApi.getTargetById(kpiTargetId!),
    enabled: kpiTargetId !== null,
  });

/** Xem trước kết thúc đơn của tổ: lệch kiểm cuối đơn theo công đoạn, kết quả KPI, tăng ca. */
export const useFinishPreview = (kpiTargetId: number | null) =>
  useQuery({
    queryKey: [...KPI_KEY, 'finish-preview', kpiTargetId],
    queryFn: () => kpiApi.finishPreview(kpiTargetId!),
    enabled: kpiTargetId !== null,
  });

/** Kết thúc đơn: chốt và đóng KPI, thông báo lệch (không tự ghi phiếu); làm mới cả hàng hỏng và đơn từ. */
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

/**
 * Chi tiết lệch kiểm cuối đơn của một chỉ tiêu (mở từ thông báo), tính lại từ dữ liệu hiện tại nên chỉ tiêu đã đóng vẫn xem được.
 * Quản đốc / Admin dùng endpoint xem trước; tổ trưởng / tổ phó dùng endpoint chỉ xem của tổ mình.
 */
export const useMismatchDetail = (role: 'manager' | 'team-leader', kpiTargetId: number | null) =>
  useQuery({
    queryKey: [...KPI_KEY, 'mismatch-detail', role, kpiTargetId],
    queryFn: () =>
      role === 'manager'
        ? kpiApi.finishPreview(kpiTargetId as number)
        : kpiApi.finishResult(kpiTargetId as number),
    enabled: kpiTargetId !== null,
  });
