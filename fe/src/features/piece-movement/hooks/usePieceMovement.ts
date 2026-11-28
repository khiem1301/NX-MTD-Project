import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ROLES } from '@/constants/roles';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useAuth } from '@/hooks/useAuth';
import { LEADER_BASE, pieceMovementApi, WORKER_BASE } from '../api/pieceMovementApi';
import { PM_MESSAGES } from '../constants';
import type {
  AllocationQuery,
  ConfirmBatchPayload,
  ConfirmOnBehalfPayload,
  SaveDailyPayload,
} from '../types';

export const PM_KEY = ['piece-movement'] as const;
// Số lần được giao là trần sản lượng và số BTP tổ giữ cũng đổi theo.
const AFFECTED = [PM_KEY, ['warehouse'], ['production']];

export const useAllocations = (query: AllocationQuery, enabled = true) => {
  const { user } = useAuth();
  const base = user?.role === ROLES.Worker ? WORKER_BASE : LEADER_BASE;
  return useQuery({
    queryKey: [...PM_KEY, 'list', base, query],
    queryFn: () => pieceMovementApi.list(base, query),
    enabled,
    placeholderData: keepPreviousData,
  });
};

/** DEV-ALL-182: phiếu của tổ gộp theo công nhân, phân trang theo công nhân. */
export const useWorkerSummaries = (query: AllocationQuery) =>
  useQuery({
    queryKey: [...PM_KEY, 'workers', query],
    queryFn: () => pieceMovementApi.workers(query),
    placeholderData: keepPreviousData,
  });

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

export const useConfirmWorkerOnBehalf = () =>
  useApiMutation(
    ({ workerId, body }: { workerId: number; body: ConfirmOnBehalfPayload }) =>
      pieceMovementApi.confirmWorkerOnBehalf(workerId, body),
    { invalidate: AFFECTED, successMessage: PM_MESSAGES.confirmed },
  );

export const useConfirmByWorker = () =>
  useApiMutation((id: number) => pieceMovementApi.confirmByWorker(id), {
    invalidate: AFFECTED,
    successMessage: PM_MESSAGES.confirmed,
  });

export const useConfirmBatchByWorker = () =>
  useApiMutation((body: ConfirmBatchPayload) => pieceMovementApi.confirmBatchByWorker(body), {
    invalidate: AFFECTED,
    successMessage: PM_MESSAGES.batchConfirmed,
  });

export const useRejectByWorker = () =>
  useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => pieceMovementApi.rejectByWorker(id, reason),
    {
      invalidate: AFFECTED,
      successMessage: PM_MESSAGES.rejected,
    },
  );
