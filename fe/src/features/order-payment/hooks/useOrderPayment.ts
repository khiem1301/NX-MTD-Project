import { useQuery } from '@tanstack/react-query';
import { useApiMutation } from '@/hooks/useApiMutation';
import { orderPaymentApi } from '../api/orderPaymentApi';
import { ORDER_PAYMENT_TEXT } from '../constants';
import type {
  AddOrderPaymentPayload,
  OrderPaymentQuery,
  SetFinalAmountPayload,
  UpdateOrderPaymentPayload,
} from '../types';

const KEY = ['order-payment'] as const;

export const useOrderPayments = (query: OrderPaymentQuery) =>
  useQuery({ queryKey: [...KEY, 'list', query], queryFn: () => orderPaymentApi.list(query) });

export const useOrderPaymentDetail = (id: number | null) =>
  useQuery({
    queryKey: [...KEY, 'detail', id],
    queryFn: () => orderPaymentApi.detail(id!),
    enabled: id !== null,
  });

const after = (successMessage: string) => ({ invalidate: [KEY], successMessage });

export const useSetFinalAmount = (id: number) =>
  useApiMutation(
    (body: SetFinalAmountPayload) => orderPaymentApi.setFinalAmount(id, body),
    after(ORDER_PAYMENT_TEXT.done.finalSaved),
  );

export const useAddOrderPayment = (id: number) =>
  useApiMutation(
    (body: AddOrderPaymentPayload) => orderPaymentApi.addPayment(id, body),
    after(ORDER_PAYMENT_TEXT.done.added),
  );

export const useUpdateOrderPayment = (id: number) =>
  useApiMutation(
    ({ paymentId, body }: { paymentId: number; body: UpdateOrderPaymentPayload }) =>
      orderPaymentApi.updatePayment(id, paymentId, body),
    after(ORDER_PAYMENT_TEXT.done.updated),
  );
