import { useEffect, useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useManagerRolePath } from '@/hooks/useRolePath';
import { cuttingApi } from '../api/cuttingApi';
import type {
  ConfirmHandoverPayload,
  CreateHandoverPayload,
  CreateReplacementPayload,
  CutHandoverQuery,
  CuttingLayQuery,
  SaveCuttingLayPayload,
  SaveWorkOrderPayload,
  CheckWorkOrderFabricPayload,
  WorkOrderFabricCheck,
  WorkOrderQuery,
} from '../types';

export const CUTTING_KEY = ['cutting'] as const;
/** Bàn giao / nhận BTP làm đổi tồn kho nên invalidate cả nhóm warehouse. */
const WAREHOUSE_KEY = ['warehouse'] as const;

export const useWorkOrders = (query: WorkOrderQuery) => {
  const role = useManagerRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'work-orders', role, query],
    queryFn: () => cuttingApi.getWorkOrders(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useReplacementRequests = (enabled: boolean) => {
  const role = useManagerRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'replacement-requests', role],
    queryFn: () => cuttingApi.getReplacementRequests(role),
    enabled,
  });
};

export const useCreateReplacement = () => {
  const role = useManagerRolePath();
  return useApiMutation(
    (body: CreateReplacementPayload) => cuttingApi.createReplacement(role, body),
    {
      invalidate: [CUTTING_KEY, WAREHOUSE_KEY, ['requests']],
      successMessage: MESSAGES.cutting.replacementCreated,
    },
  );
};

export const useWorkOrder = (id: number | undefined) => {
  const role = useManagerRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'work-order', role, id],
    queryFn: () => cuttingApi.getWorkOrder(role, id ?? 0),
    enabled: id !== undefined,
  });
};

const FABRIC_CHECK_DELAY_MS = 400;
const NO_FABRIC_CHECKS: WorkOrderFabricCheck[] = [];

/**
 * Kiểm vải khi đang lập lệnh, chưa lưu: chờ người dùng dừng gõ rồi mới hỏi máy chủ. Null = chưa đủ thông tin để kiểm,
 * khi đó trả rỗng ngay. Kết quả cũ chỉ giữ tạm khi vẫn cùng đơn và tổ cắt, để cảnh báo không nhấp nháy lúc đang gõ số.
 */
export const useWorkOrderFabricCheck = (body: CheckWorkOrderFabricPayload | null) => {
  const role = useManagerRolePath();
  const key = JSON.stringify(body);
  const [debounced, setDebounced] = useState<CheckWorkOrderFabricPayload | null>(null);
  useEffect(() => {
    const timer = setTimeout(
      () => setDebounced(JSON.parse(key) as CheckWorkOrderFabricPayload | null),
      FABRIC_CHECK_DELAY_MS,
    );
    return () => clearTimeout(timer);
  }, [key]);
  const checks = useQuery({
    queryKey: [
      ...CUTTING_KEY,
      'fabric-check',
      role,
      debounced?.orderId,
      debounced?.cuttingTeamId,
      debounced?.lines,
    ],
    queryFn: () => cuttingApi.checkWorkOrderFabric(role, debounced as CheckWorkOrderFabricPayload),
    enabled: debounced !== null,
    placeholderData: (previous, previousQuery) =>
      previousQuery?.queryKey[3] === debounced?.orderId &&
      previousQuery?.queryKey[4] === debounced?.cuttingTeamId
        ? previous
        : undefined,
  });
  return body === null ? NO_FABRIC_CHECKS : (checks.data ?? NO_FABRIC_CHECKS);
};

export const useSaveWorkOrder = () => {
  const role = useManagerRolePath();
  return useApiMutation(
    ({ id, body }: { id?: number; body: SaveWorkOrderPayload }) =>
      id ? cuttingApi.updateWorkOrder(role, id, body) : cuttingApi.createWorkOrder(role, body),
    { invalidate: [CUTTING_KEY], successMessage: MESSAGES.cutting.workOrderSaved },
  );
};

/** Tổ cắt báo cắt xong: lệnh chuyển Hoàn thành (cắt thiếu kế hoạch thì phải nêu lý do). */
export const useFinishCutting = () => {
  const role = useManagerRolePath();
  return useApiMutation(
    ({ id, reason }: { id: number; reason?: string }) => cuttingApi.finishCutting(role, id, reason),
    {
      invalidate: [CUTTING_KEY],
      successMessage: MESSAGES.cutting.workOrderFinished,
    },
  );
};

export const useCancelWorkOrder = () => {
  const role = useManagerRolePath();
  return useApiMutation((id: number) => cuttingApi.cancelWorkOrder(role, id), {
    invalidate: [CUTTING_KEY],
    successMessage: MESSAGES.cutting.workOrderCancelled,
  });
};

export const useLays = (query: CuttingLayQuery) => {
  const role = useManagerRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'lays', role, query],
    queryFn: () => cuttingApi.getLays(role, query),
    placeholderData: keepPreviousData,
  });
};

/** Lưu bàn cắt; BE trả message khi vượt kế hoạch (trong % dư) → hiện cảnh báo thay vì thông báo thường. */
export const useSaveLay = () => {
  const role = useManagerRolePath();
  return useApiMutation(
    async ({ id, body }: { id?: number; body: SaveCuttingLayPayload }) => {
      const res = id
        ? await cuttingApi.updateLay(role, id, body)
        : await cuttingApi.createLay(role, body);
      if (res.message) void message.warning(res.message);
      else void message.success(MESSAGES.cutting.laySaved);
      return res.data;
    },
    { invalidate: [CUTTING_KEY] },
  );
};

export const useHandovers = (query: CutHandoverQuery) => {
  const role = useManagerRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'handovers', role, query],
    queryFn: () => cuttingApi.getHandovers(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useHandover = (id: number | undefined) => {
  const role = useManagerRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'handover', role, id],
    queryFn: () => cuttingApi.getHandover(role, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useHandoverable = (workOrderId: number | undefined) => {
  const role = useManagerRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'handoverable', role, workOrderId],
    queryFn: () => cuttingApi.getHandoverable(role, workOrderId ?? 0),
    enabled: workOrderId !== undefined,
  });
};

export const useCreateHandover = () => {
  const role = useManagerRolePath();
  return useApiMutation((body: CreateHandoverPayload) => cuttingApi.createHandover(role, body), {
    invalidate: [CUTTING_KEY],
    successMessage: MESSAGES.cutting.handoverCreated,
  });
};

export const useConfirmHandover = () => {
  const role = useManagerRolePath();
  return useApiMutation(
    ({ id, body }: { id: number; body: ConfirmHandoverPayload }) =>
      cuttingApi.confirmHandover(role, id, body),
    {
      invalidate: [CUTTING_KEY, WAREHOUSE_KEY],
      successMessage: MESSAGES.cutting.handoverConfirmed,
    },
  );
};

export const useCancelHandover = () => {
  const role = useManagerRolePath();
  return useApiMutation((id: number) => cuttingApi.cancelHandover(role, id), {
    invalidate: [CUTTING_KEY],
    successMessage: MESSAGES.cutting.handoverCancelled,
  });
};

export const useRejectHandover = () => {
  const role = useManagerRolePath();
  return useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => cuttingApi.rejectHandover(role, id, reason),
    { invalidate: [CUTTING_KEY], successMessage: MESSAGES.cutting.handoverRejected },
  );
};
