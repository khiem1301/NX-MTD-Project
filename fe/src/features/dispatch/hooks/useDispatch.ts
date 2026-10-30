import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { ROLES } from '@/constants/roles';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useAuth } from '@/hooks/useAuth';
import { ACCOUNTANT_BASE, dispatchApi, MANAGER_BASE, WAREHOUSE_BASE } from '../api/dispatchApi';
import { DISPATCH_MESSAGES } from '../constants';
import type { ConfirmDeliveryPayload, CreateDispatchPayload, DispatchNoteQuery } from '../types';

export const DISPATCH_KEY = ['dispatch'] as const;
// Xuất hàng làm đổi tồn kho thành phẩm và trạng thái đơn hàng nên nạp lại cả hai.
const AFFECTED = [DISPATCH_KEY, ['warehouse'], ['orders']];

const useBase = () => {
  const { user } = useAuth();
  if (user?.role === ROLES.Manager) return MANAGER_BASE;
  if (user?.role === ROLES.Accountant) return ACCOUNTANT_BASE;
  return WAREHOUSE_BASE;
};

export const useDispatchNotes = (query: DispatchNoteQuery) => {
  const base = useBase();
  return useQuery({
    queryKey: [...DISPATCH_KEY, 'list', base, query],
    queryFn: () => dispatchApi.list(base, query),
    placeholderData: keepPreviousData,
  });
};

export const useDispatchNote = (id: number | undefined) => {
  const base = useBase();
  return useQuery({
    queryKey: [...DISPATCH_KEY, 'detail', base, id],
    queryFn: () => dispatchApi.detail(base, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useDispatchable = (orderId: number | undefined) =>
  useQuery({
    queryKey: [...DISPATCH_KEY, 'dispatchable', orderId],
    queryFn: () => dispatchApi.dispatchable(orderId ?? 0),
    enabled: orderId !== undefined,
  });

export const useCreateDispatch = () =>
  useApiMutation((body: CreateDispatchPayload) => dispatchApi.create(body), {
    invalidate: AFFECTED,
    successMessage: DISPATCH_MESSAGES.created,
  });

export const useCancelDispatch = () =>
  useApiMutation((id: number) => dispatchApi.cancel(id), {
    invalidate: AFFECTED,
    successMessage: DISPATCH_MESSAGES.cancelled,
  });

export const useExportDispatch = () =>
  useApiMutation((id: number) => dispatchApi.exportGoods(id), {
    invalidate: AFFECTED,
    successMessage: DISPATCH_MESSAGES.exported,
  });

export const useRejectDispatch = () =>
  useApiMutation(
    ({ id, reason }: { id: number; reason: string }) => dispatchApi.reject(id, reason),
    {
      invalidate: AFFECTED,
      successMessage: DISPATCH_MESSAGES.rejected,
    },
  );

export const useConfirmDelivery = () =>
  useApiMutation(
    ({ id, body }: { id: number; body: ConfirmDeliveryPayload }) =>
      dispatchApi.confirmDelivery(id, body),
    { invalidate: AFFECTED, successMessage: DISPATCH_MESSAGES.delivered },
  );

/** TH-16: tải Excel phiếu xuất hàng để in. */
export const useDownloadDispatch = () => {
  const base = useBase();
  return useApiMutation(({ id, code }: { id: number; code: string }) => dispatchApi.downloadExcel(base, id, code), {
    invalidate: [],
  });
};
