import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { productionApi } from '../api/productionApi';
import { PRODUCTION_TEXT } from '../constants';
import { defectsKey } from './productionKeys';
import type {
  CancelDefectPayload,
  ConfirmDefectPayload,
  CreateDefectPayload,
  DefectQuery,
  ObjectDefectPayload,
  OutputRole,
} from '../types';

// ---------- Hàng hỏng ----------

export const useDefects = (role: OutputRole, query: DefectQuery) =>
  useQuery({
    queryKey: [...defectsKey, role, 'list', query],
    queryFn: () => productionApi.listDefects(role, query),
  });

export const useCreateDefect = () =>
  useApiMutation((body: CreateDefectPayload) => productionApi.createDefect(body), {
    invalidate: [defectsKey],
    successMessage: PRODUCTION_TEXT.defects.done.created,
  });

export const useCancelDefect = (role: OutputRole) =>
  useApiMutation(
    ({ id, body }: { id: number; body: CancelDefectPayload }) =>
      productionApi.cancelDefect(role, id, body),
    {
      invalidate: [defectsKey],
      successMessage: PRODUCTION_TEXT.defects.done.cancelled,
    },
  );

/** Quản đốc / Admin chấp nhận phiếu sau khi công nhân phản đối (giữ phiếu); không chấp nhận thì hủy phiếu. */
export const useConfirmDefect = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ConfirmDefectPayload }) =>
      productionApi.confirmDefect(id, body),
    { invalidate: [defectsKey], successMessage: PRODUCTION_TEXT.defects.done.confirmed },
  );

export const useMyDefects = (query: DefectQuery) =>
  useQuery({
    queryKey: [...defectsKey, 'mine', query],
    queryFn: () => productionApi.listMyDefects(query),
  });

export const useObjectDefect = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ObjectDefectPayload }) =>
      productionApi.objectDefect(id, body),
    {
      invalidate: [defectsKey],
      successMessage: PRODUCTION_TEXT.myDefects.done,
    },
  );
