import { useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { useRolePath } from '@/hooks/useRolePath';
import { warehouseApi } from '../api/warehouseApi';
import type {
  OrderMaterialPageQuery,
  PendingMaterialOrderQuery,
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

/** Đơn chờ khai báo / còn phải mua NVL (Admin, Quản đốc). */
export const usePendingMaterialOrders = (params: PendingMaterialOrderQuery) => {
  const role = useRolePath();
  return useQuery({
    queryKey: [...WAREHOUSE_KEY, 'pending-material-orders', role, params],
    queryFn: () => warehouseApi.getPendingMaterialOrders(role, params),
  });
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
