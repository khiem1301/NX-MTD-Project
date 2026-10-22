import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import type { CreatePurchaseOrderPayload, PurchaseOrderQuery } from '../types';
import { WAREHOUSE_KEY } from './useMaterials';

export const usePurchaseOrders = (query: PurchaseOrderQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'purchase-orders', role, query],
    queryFn: () => warehouseApi.getPurchaseOrders(role, query),
    placeholderData: keepPreviousData,
  });
};

export const usePurchaseOrder = (id: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'purchase-order', role, id],
    queryFn: () => warehouseApi.getPurchaseOrder(role, id ?? 0),
    enabled: id !== undefined,
  });
};

export const useCreatePurchaseOrder = () => {
  const role = useRolePath();
  return useApiMutation(
    (body: CreatePurchaseOrderPayload) => warehouseApi.createPurchaseOrder(role, body),
    {
      // NVL mới gõ tay được thêm vào danh mục nên dropdown NVL phải nạp lại.
      invalidate: [WAREHOUSE_KEY, ['lookups', 'materials'], ['purchase', 'orders']],
      successMessage: MESSAGES.warehouse.purchaseOrderCreated,
    },
  );
};
