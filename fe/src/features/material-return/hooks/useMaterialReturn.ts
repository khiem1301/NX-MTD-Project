import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ROLES } from '@/constants/roles';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useAuth } from '@/hooks/useAuth';
import { LEADER_BASE, materialReturnApi, WAREHOUSE_BASE } from '../api/materialReturnApi';
import { RETURN_MESSAGES } from '../constants';
import type { ConfirmReturnPayload, CreateReturnPayload, MaterialReturnQuery } from '../types';

export const RETURN_KEY = ['material-return'] as const;
// Nhận vải trả làm tăng tồn NVL và giảm vải tổ cắt đang giữ.
const AFFECTED = [RETURN_KEY, ['warehouse'], ['cutting']];

const useBase = () => {
  const { user } = useAuth();
  return user?.role === ROLES.WarehouseKeeper ? WAREHOUSE_BASE : LEADER_BASE;
};

export const useMaterialReturns = (query: MaterialReturnQuery) => {
  const base = useBase();
  return useQuery({
    queryKey: [...RETURN_KEY, 'list', base, query],
    queryFn: () => materialReturnApi.list(base, query),
    placeholderData: keepPreviousData,
  });
};

export const useMaterialReturn = (id: number | undefined) => {
  const base = useBase();
  return useQuery({
    queryKey: [...RETURN_KEY, 'detail', base, id],
    queryFn: () => materialReturnApi.detail(base, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useReturnable = (enabled: boolean) =>
  useQuery({
    queryKey: [...RETURN_KEY, 'returnable'],
    queryFn: () => materialReturnApi.returnable(),
    enabled,
  });

export const useCreateReturn = () =>
  useApiMutation((body: CreateReturnPayload) => materialReturnApi.create(body), {
    invalidate: AFFECTED,
    successMessage: RETURN_MESSAGES.created,
  });

export const useCancelReturn = () =>
  useApiMutation((id: number) => materialReturnApi.cancel(id), {
    invalidate: AFFECTED,
    successMessage: RETURN_MESSAGES.cancelled,
  });

export const useConfirmReturn = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ConfirmReturnPayload }) =>
      materialReturnApi.confirm(id, body),
    { invalidate: AFFECTED, successMessage: RETURN_MESSAGES.received },
  );

export const useRejectReturn = () =>
  useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => materialReturnApi.reject(id, reason),
    {
      invalidate: AFFECTED,
      successMessage: RETURN_MESSAGES.rejected,
    },
  );
