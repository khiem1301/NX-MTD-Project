import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { lookupApi } from '@/services/lookupApi';
import { productionApi } from '../api/productionApi';
import { PRODUCTION_TEXT } from '../constants';
import { assignmentsKey, outputKey } from './productionKeys';
import type { AssignManyPayload, AssignPayload, CopyAssignmentsPayload } from '../types';

// ---------- Phân công ----------

export const useAssignments = (orderId: number | null) =>
  useQuery({
    queryKey: [...assignmentsKey, 'list', orderId],
    queryFn: () => productionApi.listAssignments(orderId!),
    enabled: orderId !== null,
  });

/** Công đoạn may đang dùng của các mã hàng trong đơn (chọn khi phân công, ghi hàng hỏng). */
export const useAssignableOperations = (orderId: number | null) =>
  useQuery({
    queryKey: [...assignmentsKey, 'operations', orderId],
    queryFn: () => productionApi.listAssignableOperations(orderId!),
    enabled: orderId !== null,
  });

/** Dòng đơn của các mã hàng được chia cho tổ, để biết cả mã chưa có công đoạn. Không dùng cache tra cứu: việc chia có thể vừa đổi. */
export const useAllottedOrderLines = (orderId: number | null) =>
  useQuery({
    queryKey: [...assignmentsKey, 'order-lines', orderId],
    queryFn: () => lookupApi.orderLines(orderId!),
    enabled: orderId !== null,
  });

export const useAssignOperation = () =>
  useApiMutation((body: AssignPayload) => productionApi.assign(body), {
    invalidate: [assignmentsKey, outputKey],
    successMessage: PRODUCTION_TEXT.assignments.done.assigned,
  });

export const useAssignOperations = () =>
  useApiMutation((body: AssignManyPayload) => productionApi.assignMany(body), {
    invalidate: [assignmentsKey, outputKey],
  });

export const useCloseAssignment = () =>
  useApiMutation((id: number) => productionApi.closeAssignment(id), {
    invalidate: [assignmentsKey, outputKey],
    successMessage: PRODUCTION_TEXT.assignments.done.closed,
  });

type SaveWorkerOperationsPayload = {
  orderId: number;
  workerId: number;
  addOperationIds: number[];
  closeAssignmentIds: number[];
};

/** Lưu một lần phần thay đổi công đoạn của một công nhân: thêm theo lô, rồi đóng từng phân công bị bỏ tích. */
export const useSaveWorkerOperations = () =>
  useApiMutation(
    async ({
      orderId,
      workerId,
      addOperationIds,
      closeAssignmentIds,
    }: SaveWorkerOperationsPayload) => {
      if (addOperationIds.length > 0)
        await productionApi.assignMany({ orderId, workerId, operationIds: addOperationIds });
      for (const id of closeAssignmentIds) await productionApi.closeAssignment(id);
    },
    {
      invalidate: [assignmentsKey, outputKey],
      successMessage: PRODUCTION_TEXT.assignments.byWorker.saved,
    },
  );

export const useCopyAssignments = () =>
  useApiMutation((body: CopyAssignmentsPayload) => productionApi.copyAssignments(body), {
    invalidate: [assignmentsKey, outputKey],
  });
