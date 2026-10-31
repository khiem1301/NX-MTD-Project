import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import type { MaterialReceiptQuery, SaveReceiptPayload } from '../types';
import { WAREHOUSE_KEY } from './useMaterials';

export const useReceipts = (query: MaterialReceiptQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'material-receipts', role, query],
    queryFn: () => warehouseApi.getReceipts(role, query),
    placeholderData: keepPreviousData,
  });
};

export const useReceipt = (id: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'material-receipt', role, id],
    queryFn: () => warehouseApi.getReceipt(role, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useSaveReceipt = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, body }: { id?: number; body: SaveReceiptPayload }) =>
      id ? warehouseApi.updateReceipt(role, id, body) : warehouseApi.createReceipt(role, body),
    { invalidate: [WAREHOUSE_KEY], successMessage: MESSAGES.warehouse.receiptSaved },
  );
};

export const useConfirmReceipt = () => {
  const role = useRolePath();
  return useApiMutation((id: number) => warehouseApi.confirmReceipt(role, id), {
    invalidate: [WAREHOUSE_KEY],
    successMessage: MESSAGES.warehouse.receiptConfirmed,
  });
};

export const useReturnReceipt = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, reason }: { id: number; reason: string }) =>
      warehouseApi.returnReceipt(role, id, reason),
    { invalidate: [WAREHOUSE_KEY], successMessage: MESSAGES.warehouse.receiptReturned },
  );
};

/** TH-16: tải Excel phiếu nhập để in; lỗi hiện thông báo như các thao tác khác. */
export const useDownloadReceipt = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, code }: { id: number; code: string }) => warehouseApi.downloadReceipt(role, id, code),
    {
      invalidate: [],
    },
  );
};
