import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import { ISSUABLE_WORK_ORDER_STATUSES } from '../constants';
import type { ConfirmIssuePayload, CreateIssuePayload, MaterialIssueQuery } from '../types';
import { WAREHOUSE_KEY } from './useMaterials';

/** Cấp vải làm đổi số "đã cấp / còn phải cấp" của lệnh cắt nên invalidate cả nhóm cutting. */
const CUTTING_KEY = ['cutting'] as const;

export const useIssues = (query: MaterialIssueQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'material-issues', role, query],
    queryFn: () => warehouseApi.getIssues(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useIssue = (id: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'material-issue', role, id],
    queryFn: () => warehouseApi.getIssue(role, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useCreateIssue = () => {
  const role = useRolePath();
  return useApiMutation((body: CreateIssuePayload) => warehouseApi.createIssue(role, body), {
    invalidate: [WAREHOUSE_KEY, CUTTING_KEY],
    successMessage: MESSAGES.warehouse.issueCreated,
  });
};

export const useConfirmIssue = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, body }: { id: number; body: ConfirmIssuePayload }) =>
      warehouseApi.confirmIssue(role, id, body),
    { invalidate: [WAREHOUSE_KEY, CUTTING_KEY], successMessage: MESSAGES.warehouse.issueConfirmed },
  );
};

export const useRejectIssue = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => warehouseApi.rejectIssue(role, id, reason),
    { invalidate: [WAREHOUSE_KEY, CUTTING_KEY], successMessage: MESSAGES.warehouse.issueRejected },
  );
};

export const useIssuableWorkOrders = (enabled: boolean) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'work-orders', role, 'issuable'],
    queryFn: async () =>
      (await warehouseApi.getRecentWorkOrders(role)).items.filter((w) =>
        (ISSUABLE_WORK_ORDER_STATUSES as readonly string[]).includes(w.status),
      ),
    enabled,
  });
};

export const useWorkOrderFabrics = (workOrderId: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...CUTTING_KEY, 'work-order-fabrics', role, workOrderId],
    queryFn: () => warehouseApi.getWorkOrderFabrics(role, workOrderId ?? 0),
    enabled: workOrderId !== undefined,
  });
};

/** TH-16: tải Excel phiếu cấp để in. */
export const useDownloadIssue = () => {
  const role = useRolePath();
  return useApiMutation(({ id, code }: { id: number; code: string }) => warehouseApi.downloadIssue(role, id, code), {
    invalidate: [],
  });
};
