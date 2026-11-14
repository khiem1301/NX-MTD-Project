import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { productionApi } from '../api/productionApi';
import { PRODUCTION_TEXT } from '../constants';
import { defectsKey } from './productionKeys';
import type {
  CancelDefectPayload,
  CreateDefectPayload,
  DefectQuery,
  OutputRole,
  UpdateDefectPayload,
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

/** Sửa phiếu: tổ trưởng trong 24 giờ kể từ lúc lập, Quản đốc khi chưa vướng ràng buộc (BE kiểm). */
export const useUpdateDefect = (role: OutputRole) =>
  useApiMutation(
    ({ id, body }: { id: number; body: UpdateDefectPayload }) =>
      productionApi.updateDefect(role, id, body),
    { invalidate: [defectsKey], successMessage: PRODUCTION_TEXT.defects.done.updated },
  );

export const useCancelDefect = (role: OutputRole) =>
  useApiMutation(
    ({ id, body }: { id: number; body: CancelDefectPayload }) =>
      productionApi.cancelDefect(role, id, body),
    {
      invalidate: [defectsKey],
      successMessage: PRODUCTION_TEXT.defects.done.cancelled,
    },
  );

/** Công nhân chỉ xem phiếu của mình. */
export const useMyDefects = (query: DefectQuery) =>
  useQuery({
    queryKey: [...defectsKey, 'mine', query],
    queryFn: () => productionApi.listMyDefects(query),
  });
