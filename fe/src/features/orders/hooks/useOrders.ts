import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { MESSAGES } from '@/constants/messages';
import { useApiMutation } from '@/hooks/useApiMutation';
import { customerApi, orderApi, productApi } from '../api/orderApi';
import type {
  CreateOrderPayload,
  OrderQuery,
  ProductQuery,
  SaveCustomerPayload,
  SaveProductPayload,
  UpdateOrderPayload,
} from '../types';

export const ORDERS_KEY = ['orders'] as const;
const PRODUCTS_KEY = [...ORDERS_KEY, 'products'] as const;
const CUSTOMERS_KEY = [...ORDERS_KEY, 'customers'] as const;
/** Mã hàng / khách / đơn đổi thì dropdown tra cứu của luồng khác cũng phải tải lại. */
const LOOKUPS_KEY = ['order-lookups'] as const;

export const useCustomers = (keyword?: string) =>
  useQuery({ queryKey: [...CUSTOMERS_KEY, keyword], queryFn: () => customerApi.list(keyword) });

export const useSaveCustomer = () =>
  useApiMutation(
    ({ id, body }: { id?: number; body: SaveCustomerPayload }) =>
      id === undefined ? customerApi.create(body) : customerApi.update(id, body).then(() => id),
    { invalidate: [CUSTOMERS_KEY], successMessage: MESSAGES.common.saved },
  );

export const useProducts = (query: ProductQuery) =>
  useQuery({
    queryKey: [...PRODUCTS_KEY, query],
    queryFn: () => productApi.list(query),
    placeholderData: keepPreviousData,
  });

export const useSaveProduct = () =>
  useApiMutation(
    ({ id, body }: { id?: number; body: SaveProductPayload }) =>
      id === undefined ? productApi.create(body) : productApi.update(id, body).then(() => id),
    { invalidate: [PRODUCTS_KEY, LOOKUPS_KEY], successMessage: MESSAGES.common.saved },
  );

export const useDiscontinueProduct = () =>
  useApiMutation((id: number) => productApi.discontinue(id), {
    invalidate: [PRODUCTS_KEY, LOOKUPS_KEY],
    successMessage: MESSAGES.common.saved,
  });

export const useReactivateProduct = () =>
  useApiMutation((id: number) => productApi.reactivate(id), {
    invalidate: [PRODUCTS_KEY, LOOKUPS_KEY],
    successMessage: MESSAGES.common.saved,
  });

export const useOrders = (query: OrderQuery) =>
  useQuery({
    queryKey: [...ORDERS_KEY, 'list', query],
    queryFn: () => orderApi.list(query),
    placeholderData: keepPreviousData,
  });

export const useOrder = (id: number | null) =>
  useQuery({
    queryKey: [...ORDERS_KEY, 'detail', id],
    queryFn: () => orderApi.get(id as number),
    enabled: id !== null,
  });

/** Mã gợi ý chỉ lấy khi mở form tạo đơn; không cache để mã luôn mới. */
export const useNextOrderCode = (enabled: boolean) =>
  useQuery({
    queryKey: [...ORDERS_KEY, 'next-code'],
    queryFn: orderApi.nextCode,
    enabled,
    gcTime: 0,
    staleTime: 0,
  });

export const useCreateOrder = () =>
  useApiMutation((body: CreateOrderPayload) => orderApi.create(body), {
    invalidate: [ORDERS_KEY, LOOKUPS_KEY],
    successMessage: MESSAGES.common.saved,
  });

export const useUpdateOrder = (id: number) =>
  useApiMutation((body: UpdateOrderPayload) => orderApi.update(id, body), {
    invalidate: [ORDERS_KEY, LOOKUPS_KEY],
    successMessage: MESSAGES.common.saved,
  });

export const useCancelOrder = (id: number) =>
  useApiMutation(({ reason, rowVer }: { reason: string | null; rowVer: string }) => orderApi.cancel(id, reason, rowVer), {
    invalidate: [ORDERS_KEY, LOOKUPS_KEY],
  });

export const useStartProduction = (id: number) =>
  useApiMutation((rowVer: string) => orderApi.startProduction(id, rowVer), {
    invalidate: [ORDERS_KEY, LOOKUPS_KEY],
    successMessage: MESSAGES.orders.productionStarted,
  });

export const useStopOrder = (id: number) =>
  useApiMutation(({ reason, rowVer }: { reason: string; rowVer: string }) => orderApi.stop(id, reason, rowVer), {
    invalidate: [ORDERS_KEY, LOOKUPS_KEY],
  });
