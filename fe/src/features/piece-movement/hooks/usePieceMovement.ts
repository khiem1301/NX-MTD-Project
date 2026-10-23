import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ROLES } from '@/constants/roles';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useAuth } from '@/hooks/useAuth';
import { LEADER_BASE, pieceMovementApi, WORKER_BASE } from '../api/pieceMovementApi';
import { PM_MESSAGES } from '../constants';
import type {
  ConfirmMovementPayload,
  ConfirmOnBehalfPayload,
  CreateMovementPayload,
  MovementQuery,
} from '../types';

export const PM_KEY = ['piece-movement'] as const;
// BTP công nhân giữ đổi thì trần sản lượng và BTP tổ giữ cũng đổi.
const AFFECTED = [PM_KEY, ['warehouse'], ['production']];

export const useMovements = (query: MovementQuery) => {
  const { user } = useAuth();
  const base = user?.role === ROLES.Worker ? WORKER_BASE : LEADER_BASE;
  return useQuery({
    queryKey: [...PM_KEY, 'list', base, query],
    queryFn: () => pieceMovementApi.list(base, query),
    placeholderData: keepPreviousData,
  });
};

export const useHoldings = (enabled: boolean) =>
  useQuery({
    queryKey: [...PM_KEY, 'holdings'],
    queryFn: () => pieceMovementApi.holdings(),
    enabled,
  });

export const useCreateMovement = () =>
  useApiMutation((body: CreateMovementPayload) => pieceMovementApi.create(body), {
    invalidate: AFFECTED,
    successMessage: PM_MESSAGES.created,
  });

export const useConfirmOnBehalf = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ConfirmOnBehalfPayload }) =>
      pieceMovementApi.confirmOnBehalf(id, body),
    { invalidate: AFFECTED, successMessage: PM_MESSAGES.confirmed },
  );

export const useConfirmByWorker = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ConfirmMovementPayload }) =>
      pieceMovementApi.confirmByWorker(id, body),
    { invalidate: AFFECTED, successMessage: PM_MESSAGES.confirmed },
  );

export const useRejectByWorker = () =>
  useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => pieceMovementApi.rejectByWorker(id, reason),
    {
      invalidate: AFFECTED,
      successMessage: PM_MESSAGES.rejected,
    },
  );
