import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  CreateOrderPayload,
  Customer,
  OrderDetail,
  OrderListItem,
  OrderQuery,
  ProductDetail,
  ProductListItem,
  ProductQuery,
  SaveCustomerPayload,
  SaveProductPayload,
  UpdateOrderPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

export const customerApi = {
  list: (keyword?: string) => get<Customer[]>(API.customers.base, { keyword }),
  create: (body: SaveCustomerPayload) => post<number>(API.customers.base, body),
  update: (id: number, body: SaveCustomerPayload) => put<unknown>(API.customers.byId(id), body),
};

export const productApi = {
  list: (q: ProductQuery) => get<Paginated<ProductListItem>>(API.products.base, q),
  get: (id: number) => get<ProductDetail>(API.products.byId(id)),
  create: (body: SaveProductPayload) => post<number>(API.products.base, body),
  update: (id: number, body: SaveProductPayload) => put<unknown>(API.products.byId(id), body),
  discontinue: (id: number) => post<unknown>(API.products.discontinue(id)),
  reactivate: (id: number) => post<unknown>(API.products.reactivate(id)),
};

export const orderApi = {
  list: (q: OrderQuery) => get<Paginated<OrderListItem>>(API.customerOrders.base, q),
  nextCode: () => get<string>(API.customerOrders.nextCode),
  get: (id: number) => get<OrderDetail>(API.customerOrders.byId(id)),
  create: (body: CreateOrderPayload) => post<number>(API.customerOrders.base, body),
  update: (id: number, body: UpdateOrderPayload) => put<unknown>(API.customerOrders.byId(id), body),
  cancel: (id: number, reason: string | null, rowVer: string) =>
    post<unknown>(API.customerOrders.cancel(id), { reason, rowVer }),
  stop: (id: number, reason: string, rowVer: string) =>
    post<unknown>(API.customerOrders.stop(id), { reason, rowVer }),
};
