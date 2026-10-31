import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { PURCHASE_TEXT } from '../constants';
import { purchaseApi } from '../api/purchaseApi';
import type {
  ConfirmPaymentPayload,
  PlacePayload,
  PurchaseOrderQuery,
  PurchaseRole,
  ReasonPayload,
  RepricePayload,
  SaveSupplierPayload,
  SupplierQuery,
} from '../types';

export const PURCHASE_KEY = ['purchase'] as const;
const suppliersKey = [...PURCHASE_KEY, 'suppliers'] as const;
const ordersKey = [...PURCHASE_KEY, 'orders'] as const;

// ---------- Nhà cung cấp ----------

export const useSuppliers = (query: SupplierQuery) =>
  useQuery({
    queryKey: [...suppliersKey, 'list', query],
    queryFn: () => purchaseApi.listSuppliers(query),
  });

/** Nhà cung cấp đang hợp tác cho ô chọn của Quản đốc. */
export const useSupplierOptions = (enabled: boolean) =>
  useQuery({
    queryKey: [...suppliersKey, 'options'],
    queryFn: purchaseApi.supplierOptions,
    enabled,
  });

export const useSaveSupplier = (id: number | null) =>
  useApiMutation(
    async (body: SaveSupplierPayload): Promise<void> => {
      if (id === null) await purchaseApi.createSupplier(body);
      else await purchaseApi.updateSupplier(id, body);
    },
    { invalidate: [suppliersKey], successMessage: PURCHASE_TEXT.saved },
  );

export const useToggleSupplier = () =>
  useApiMutation(
    ({ id, discontinue }: { id: number; discontinue: boolean }) =>
      discontinue ? purchaseApi.discontinueSupplier(id) : purchaseApi.reactivateSupplier(id),
    { invalidate: [suppliersKey], successMessage: PURCHASE_TEXT.saved },
  );

// ---------- Đơn mua ----------

export const usePurchaseOrders = (role: PurchaseRole, query: PurchaseOrderQuery) =>
  useQuery({
    queryKey: [...ordersKey, role, 'list', query],
    queryFn: () => purchaseApi.listOrders(role, query),
  });

export const usePurchaseOrderDetail = (role: PurchaseRole, id: number | null) =>
  useQuery({
    queryKey: [...ordersKey, role, 'detail', id],
    queryFn: () => purchaseApi.getOrder(role, id!),
    enabled: id !== null,
  });

const afterAction = (successMessage: string) => ({ invalidate: [ordersKey], successMessage });

export const usePlaceOrder = (id: number) =>
  useApiMutation(
    (body: PlacePayload) => purchaseApi.place(id, body),
    afterAction(PURCHASE_TEXT.done.placed),
  );

export const useCancelOrder = (id: number) =>
  useApiMutation(
    (body: ReasonPayload) => purchaseApi.cancel(id, body),
    afterAction(PURCHASE_TEXT.done.cancelled),
  );

export const useRepriceOrder = (id: number) =>
  useApiMutation(
    (body: RepricePayload) => purchaseApi.reprice(id, body),
    afterAction(PURCHASE_TEXT.done.repriced),
  );

export const useConfirmPayment = (id: number) =>
  useApiMutation(
    (body: ConfirmPaymentPayload) => purchaseApi.confirmPayment(id, body),
    afterAction(PURCHASE_TEXT.done.paid),
  );

export const useReturnPayment = (id: number) =>
  useApiMutation(
    (body: ReasonPayload) => purchaseApi.returnPayment(id, body),
    afterAction(PURCHASE_TEXT.done.returned),
  );
