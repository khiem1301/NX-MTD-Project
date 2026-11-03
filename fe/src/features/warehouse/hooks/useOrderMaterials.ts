import { useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import type {
  CreateOrderMaterialsPayload,
  OrderMaterialPageQuery,
  SaveOrderMaterialPayload,
} from '../types';
import { WAREHOUSE_KEY } from './useMaterials';

export const useOrderMaterials = (orderId: number | undefined) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'order-materials', role, orderId],
    queryFn: () => warehouseApi.getOrderMaterials(role, orderId ?? 0),
    enabled: orderId !== undefined,
  });
};

/** Bảng NVL của đơn (Admin, Quản đốc): lọc và phân trang ở server. */
export const useOrderMaterialPage = (params: OrderMaterialPageQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'order-materials', role, 'page', params],
    queryFn: () => warehouseApi.getOrderMaterialPage(role, params),
  });
};

/** Mã hàng trong đơn kèm NVL định mức, để thêm NVL theo mã hàng. */
export const useOrderProductMaterials = (orderId: number) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'order-materials', role, 'products', orderId],
    queryFn: () => warehouseApi.getOrderProductMaterials(role, orderId),
  });
};

export const useCreateOrderMaterials = () => {
  const role = useRolePath();
  return useApiMutation(
    (body: CreateOrderMaterialsPayload) => warehouseApi.createOrderMaterials(role, body),
    { invalidate: [WAREHOUSE_KEY], successMessage: MESSAGES.warehouse.orderMaterialSaved },
  );
};

export const useSaveOrderMaterial = () => {
  const role = useRolePath();
  return useApiMutation(
    ({ id, body }: { id?: number; body: SaveOrderMaterialPayload }) =>
      id
        ? warehouseApi.updateOrderMaterial(role, id, body)
        : warehouseApi.createOrderMaterial(role, body),
    { invalidate: [WAREHOUSE_KEY], successMessage: MESSAGES.warehouse.orderMaterialSaved },
  );
};

export const useDeleteOrderMaterial = () => {
  const role = useRolePath();
  return useApiMutation((id: number) => warehouseApi.deleteOrderMaterial(role, id), {
    invalidate: [WAREHOUSE_KEY],
    successMessage: MESSAGES.warehouse.orderMaterialDeleted,
  });
};
