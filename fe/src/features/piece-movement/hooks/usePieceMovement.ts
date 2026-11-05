import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ROLES } from '@/constants/roles';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useAuth } from '@/hooks/useAuth';
import { LEADER_BASE, pieceMovementApi, WORKER_BASE } from '../api/pieceMovementApi';
import { PM_MESSAGES } from '../constants';
import type {
  AllocationQuery,
  ConfirmAllocationPayload,
  ConfirmOnBehalfPayload,
  SaveDailyPayload,
} from '../types';

export const PM_KEY = ['piece-movement'] as const;
// Số lần được giao là trần sản lượng và số BTP tổ giữ cũng đổi theo.
const AFFECTED = [PM_KEY, ['warehouse'], ['production']];

export const useAllocations = (query: AllocationQuery) => {
  const { user } = useAuth();
  const base = user?.role === ROLES.Worker ? WORKER_BASE : LEADER_BASE;
  return useQuery({
    queryKey: [...PM_KEY, 'list', base, query],
    queryFn: () => pieceMovementApi.list(base, query),
    placeholderData: keepPreviousData,
  });
};

export const useBoard = (orderId: number | undefined) =>
  useQuery({
    queryKey: [...PM_KEY, 'board', orderId],
    queryFn: () => pieceMovementApi.board(orderId),
    enabled: orderId !== undefined,
  });

/** Bảng giao việc của một dòng đơn (màu × size) trong ngày; chưa chọn dòng đơn thì không tải (DEV-ALL-68). */
export const useDailyBoard = (
  workDate: string,
  orderId: number | undefined,
  orderLineId: number | undefined,
) =>
  useQuery({
    queryKey: [...PM_KEY, 'daily', workDate, orderId, orderLineId],
    queryFn: () => pieceMovementApi.daily(workDate, orderId as number, orderLineId as number),
    enabled: orderId !== undefined && orderLineId !== undefined,
  });

export const useSaveDaily = () =>
  useApiMutation((body: SaveDailyPayload) => pieceMovementApi.saveDaily(body), {
    invalidate: AFFECTED,
    successMessage: PM_MESSAGES.dailySaved,
  });

export const useConfirmOnBehalf = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ConfirmOnBehalfPayload }) =>
      pieceMovementApi.confirmOnBehalf(id, body),
    { invalidate: AFFECTED, successMessage: PM_MESSAGES.confirmed },
  );

export const useConfirmByWorker = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ConfirmAllocationPayload }) =>
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
