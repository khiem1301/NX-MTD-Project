import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ROLES } from '@/constants/roles';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useAuth } from '@/hooks/useAuth';
import { fgHandoverApi, QA_QC_BASE, WAREHOUSE_BASE } from '../api/fgHandoverApi';
import { FG_MESSAGES } from '../constants';
import type { ConfirmFgHandoverPayload, FgHandoverQuery } from '../types';

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

/** Chỉ tải khi mở hộp bàn giao (enabled). */
export const useTeamHandoverable = (enabled: boolean) =>
  useQuery({
    queryKey: [...FG_KEY, 'team-handoverable'],
    queryFn: () => fgHandoverApi.teamHandoverable(),
    enabled,
  });

export const useHandoverAll = () =>
  useApiMutation(() => fgHandoverApi.handoverAll(), { invalidate: AFFECTED });

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
