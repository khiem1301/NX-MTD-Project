import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { productionApi } from '../api/productionApi';
import { PRODUCTION_TEXT } from '../constants';
import { operationsKey, pieceRatesKey } from './productionKeys';
import type { PieceRateQuery, PieceRateRole, SavePieceRatePayload } from '../types';

// ---------- Đơn giá ----------

export const usePieceRates = (role: PieceRateRole, query: PieceRateQuery) =>
  useQuery({
    queryKey: [...pieceRatesKey, role, query],
    queryFn: () => productionApi.listPieceRates(role, query),
  });

export const useSavePieceRate = (id: number | null) =>
  useApiMutation(
    async (body: SavePieceRatePayload): Promise<void> => {
      if (id === null) await productionApi.createPieceRate(body);
      else await productionApi.updatePieceRate(id, body);
    },
    { invalidate: [pieceRatesKey, operationsKey], successMessage: PRODUCTION_TEXT.saved },
  );

export const useSubmitPieceRate = () =>
  useApiMutation((id: number) => productionApi.submitPieceRate(id), {
    invalidate: [pieceRatesKey, operationsKey],
    successMessage: PRODUCTION_TEXT.pieceRates.done.submitted,
  });

export const useSubmitPieceRates = () =>
  useApiMutation((ids: number[]) => productionApi.submitPieceRates(ids), {
    invalidate: [pieceRatesKey, operationsKey],
  });

export const useApprovePieceRate = () =>
  useApiMutation(
    ({ id, note }: { id: number; note?: string }) => productionApi.approvePieceRate(id, note),
    {
      invalidate: [pieceRatesKey, operationsKey],
      successMessage: PRODUCTION_TEXT.pieceRates.done.approved,
    },
  );

export const useApproveManyPieceRates = () =>
  useApiMutation(
    ({ ids, note }: { ids: number[]; note?: string }) =>
      productionApi.approveManyPieceRates(ids, note),
    {
      invalidate: [pieceRatesKey, operationsKey],
    },
  );

export const useRejectPieceRate = () =>
  useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => productionApi.rejectPieceRate(id, reason),
    {
      invalidate: [pieceRatesKey],
      successMessage: PRODUCTION_TEXT.pieceRates.done.rejected,
    },
  );
