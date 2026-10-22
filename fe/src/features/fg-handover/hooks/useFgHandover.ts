import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ROLES } from '@/constants/roles';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useAuth } from '@/hooks/useAuth';
import { fgHandoverApi, QA_QC_BASE, WAREHOUSE_BASE } from '../api/fgHandoverApi';
import { FG_MESSAGES } from '../constants';
import type { ConfirmFgHandoverPayload, CreateFgHandoverPayload, FgHandoverQuery } from '../types';

export const FG_KEY = ['fg-handover'] as const;
// Phiếu nhập kho thành phẩm làm đổi tồn kho nên cũng nạp lại dữ liệu kho.
const AFFECTED = [FG_KEY, ['warehouse']];

const useBase = () => {
  const { user } = useAuth();
  return user?.role === ROLES.QaQc ? QA_QC_BASE : WAREHOUSE_BASE;
};

export const useFgHandovers = (query: FgHandoverQuery) => {
  const base = useBase();
  return useQuery({
    queryKey: [...FG_KEY, 'list', base, query],
    queryFn: () => fgHandoverApi.list(base, query),
    placeholderData: keepPreviousData,
  });
};

export const useFgHandover = (id: number | undefined) => {
  const base = useBase();
  return useQuery({
    queryKey: [...FG_KEY, 'detail', base, id],
    queryFn: () => fgHandoverApi.detail(base, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useFgHandoverable = (orderId: number | undefined) =>
  useQuery({
    queryKey: [...FG_KEY, 'handoverable', orderId],
    queryFn: () => fgHandoverApi.handoverable(orderId ?? 0),
    enabled: orderId !== undefined,
  });

export const useCreateFgHandover = () =>
  useApiMutation((body: CreateFgHandoverPayload) => fgHandoverApi.create(body), {
    invalidate: AFFECTED,
    successMessage: FG_MESSAGES.created,
  });

export const useCancelFgHandover = () =>
  useApiMutation((id: number) => fgHandoverApi.cancel(id), {
    invalidate: AFFECTED,
    successMessage: FG_MESSAGES.cancelled,
  });

export const useConfirmFgHandover = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ConfirmFgHandoverPayload }) =>
      fgHandoverApi.confirm(id, body),
    { invalidate: AFFECTED, successMessage: FG_MESSAGES.received },
  );

export const useRejectFgHandover = () =>
  useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => fgHandoverApi.reject(id, reason),
    {
      invalidate: AFFECTED,
      successMessage: FG_MESSAGES.rejected,
    },
  );
