import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { productionApi } from '../api/productionApi';
import { PRODUCTION_TEXT } from '../constants';
import { qcKey } from './productionKeys';
import type {
  CreateQcBatchPayload,
  DeleteQcBatchPayload,
  QcBatchQuery,
  QcRole,
  UpdateQcBatchPayload,
} from '../types';

// ---------- QC ----------

export const useQcBatches = (role: QcRole, query: QcBatchQuery) =>
  useQuery({
    queryKey: [...qcKey, role, 'list', query],
    queryFn: () => productionApi.listQcBatches(role, query),
  });

export const useOnLineQty = (orderLineId: number | null) =>
  useQuery({
    queryKey: [...qcKey, 'on-line', orderLineId],
    queryFn: () => productionApi.getOnLine(orderLineId!),
    enabled: orderLineId !== null,
  });

export const useCreateQcBatch = () =>
  useApiMutation((body: CreateQcBatchPayload) => productionApi.createQcBatch(body), {
    invalidate: [qcKey],
    successMessage: PRODUCTION_TEXT.qc.done.created,
  });

export const useUpdateQcBatch = (role: QcRole) =>
  useApiMutation(
    ({ id, body }: { id: number; body: UpdateQcBatchPayload }) =>
      productionApi.updateQcBatch(role, id, body),
    {
      invalidate: [qcKey],
      successMessage: PRODUCTION_TEXT.qc.done.updated,
    },
  );

export const useDeleteQcBatch = (role: QcRole) =>
  useApiMutation(
    ({ id, body }: { id: number; body: DeleteQcBatchPayload }) =>
      productionApi.deleteQcBatch(role, id, body),
    {
      invalidate: [qcKey],
      successMessage: PRODUCTION_TEXT.qc.done.deleted,
    },
  );
