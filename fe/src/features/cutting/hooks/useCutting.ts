import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { message } from 'antd';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { cuttingApi } from '../api/cuttingApi';
import type {
  ConfirmHandoverPayload,
  CreateHandoverPayload,
  CreateReplacementPayload,
  CutHandoverQuery,
  CuttingLayQuery,
  SaveCuttingLayPayload,
  SaveWorkOrderPayload,
  WorkOrderQuery,
} from '../types';

export const CUTTING_KEY = ['cutting'] as const;
/** Bàn giao / nhận BTP làm đổi tồn kho nên invalidate cả nhóm warehouse. */
const WAREHOUSE_KEY = ['warehouse'] as const;

export const useWorkOrders = (query: WorkOrderQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'work-orders', role, query],
    queryFn: () => cuttingApi.getWorkOrders(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useReplacementRequests = (enabled: boolean) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'replacement-requests', role],
    queryFn: () => cuttingApi.getReplacementRequests(role),
    enabled,
  });
};

export const useCreateReplacement = () => {
  const role = useRolePath();
  return useApiMutation(
    (body: CreateReplacementPayload) => cuttingApi.createReplacement(role, body),
    {
      invalidate: [CUTTING_KEY, WAREHOUSE_KEY, ['requests']],
      successMessage: MESSAGES.cutting.replacementCreated,
    },
  );
};

export const useWorkOrder = (id: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'work-order', role, id],
    queryFn: () => cuttingApi.getWorkOrder(role, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useSaveWorkOrder = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, body }: { id?: number; body: SaveWorkOrderPayload }) =>
      id ? cuttingApi.updateWorkOrder(role, id, body) : cuttingApi.createWorkOrder(role, body),
    { invalidate: [CUTTING_KEY], successMessage: MESSAGES.cutting.workOrderSaved },
  );
};

export const useCancelWorkOrder = () => {
  const role = useRolePath();
  return useApiMutation((id: number) => cuttingApi.cancelWorkOrder(role, id), {
    invalidate: [CUTTING_KEY],
    successMessage: MESSAGES.cutting.workOrderCancelled,
  });
};

export const useLays = (query: CuttingLayQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'lays', role, query],
    queryFn: () => cuttingApi.getLays(role, query),
    placeholderData: keepPreviousData,
  });
};

/** Lưu bàn cắt; BE trả message khi vượt kế hoạch (trong % dư) → hiện cảnh báo thay vì thông báo thường. */
export const useSaveLay = () => {
  const role = useRolePath();
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
  const role = useRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'handovers', role, query],
    queryFn: () => cuttingApi.getHandovers(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useHandover = (id: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'handover', role, id],
    queryFn: () => cuttingApi.getHandover(role, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useHandoverable = (workOrderId: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'handoverable', role, workOrderId],
    queryFn: () => cuttingApi.getHandoverable(role, workOrderId ?? 0),
    enabled: workOrderId !== undefined,
  });
};

export const useCreateHandover = () => {
  const role = useRolePath();
  return useApiMutation((body: CreateHandoverPayload) => cuttingApi.createHandover(role, body), {
    invalidate: [CUTTING_KEY],
    successMessage: MESSAGES.cutting.handoverCreated,
  });
};

export const useConfirmHandover = () => {
  const role = useRolePath();
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
  const role = useRolePath();
  return useApiMutation((id: number) => cuttingApi.cancelHandover(role, id), {
    invalidate: [CUTTING_KEY],
    successMessage: MESSAGES.cutting.handoverCancelled,
  });
};

export const useRejectHandover = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => cuttingApi.rejectHandover(role, id, reason),
    { invalidate: [CUTTING_KEY], successMessage: MESSAGES.cutting.handoverRejected },
  );
};
