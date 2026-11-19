import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  AddOrderPaymentPayload,
  OrderPaymentDetail,
  OrderPaymentListItem,
  OrderPaymentQuery,
  SetFinalAmountPayload,
  UpdateOrderPaymentPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

export const orderPaymentApi = {
  list: (params: OrderPaymentQuery) =>
    get<Paginated<OrderPaymentListItem>>(API.orderPayment.list, params),
  detail: (id: number) => get<OrderPaymentDetail>(API.orderPayment.detail(id)),
  setFinalAmount: (id: number, body: SetFinalAmountPayload) =>
    put<void>(API.orderPayment.finalAmount(id), body),
  addPayment: (id: number, body: AddOrderPaymentPayload) =>
    post<void>(API.orderPayment.payments(id), body),
  updatePayment: (id: number, paymentId: number, body: UpdateOrderPaymentPayload) =>
    put<void>(API.orderPayment.payment(id, paymentId), body),
};
