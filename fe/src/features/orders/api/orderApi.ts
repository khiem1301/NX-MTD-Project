import { API, type ProductScope } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  CreateOrderPayload,
  Customer,
  CustomerQuery,
  OrderDetail,
  OrderListItem,
  OrderQuery,
  ProductDetail,
  ProductListItem,
  ProductQuery,
  ProductSpec,
  SaveCustomerPayload,
  SaveProductPayload,
  SaveProductSpecPayload,
  UpdateOrderPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

export const customerApi = {
  list: (query: CustomerQuery = {}) => get<Customer[]>(API.customers.base, query),
  create: (body: SaveCustomerPayload) => post<number>(API.customers.base, body),
  update: (id: number, body: SaveCustomerPayload) => put<unknown>(API.customers.byId(id), body),
  discontinue: (id: number) => post<void>(API.customers.discontinue(id)),
  reactivate: (id: number) => post<void>(API.customers.reactivate(id)),
};

export const productApi = {
  uploadSampleImage: async (file: File): Promise<string> => {
    const body = new FormData();
    body.append('file', file);
    return post<string>(API.products.sampleImage(), body);
  },
  sampleImageBlob: async (key: string, scope: ProductScope = 'admin'): Promise<Blob> =>
    (await apiClient.get<Blob>(API.products.sampleImage(scope), { params: { key }, responseType: 'blob' })).data,
  list: (q: ProductQuery, scope: ProductScope = 'admin') => get<Paginated<ProductListItem>>(API.products.base(scope), q),
  get: (id: number) => get<ProductDetail>(API.products.byId(id)),
  create: (body: SaveProductPayload) => post<number>(API.products.base(), body),
  update: (id: number, body: SaveProductPayload) => put<unknown>(API.products.byId(id), body),
  discontinue: (id: number) => post<unknown>(API.products.discontinue(id)),
  reactivate: (id: number) => post<unknown>(API.products.reactivate(id)),
  getSpec: (id: number, scope: ProductScope = 'admin') => get<ProductSpec>(API.products.spec(id, scope)),
  saveSpec: (id: number, body: SaveProductSpecPayload, scope: ProductScope = 'admin') =>
    put<ProductSpec>(API.products.spec(id, scope), body),
};

export const orderApi = {
  list: (q: OrderQuery) => get<Paginated<OrderListItem>>(API.customerOrders.base, q),
  nextCode: () => get<string>(API.customerOrders.nextCode),
  get: (id: number) => get<OrderDetail>(API.customerOrders.byId(id)),
  create: (body: CreateOrderPayload) => post<number>(API.customerOrders.base, body),
  update: (id: number, body: UpdateOrderPayload) => put<unknown>(API.customerOrders.byId(id), body),
  cancel: (id: number, reason: string | null, rowVer: string) =>
    post<unknown>(API.customerOrders.cancel(id), { reason, rowVer }),
  startProduction: (id: number, rowVer: string) =>
    post<unknown>(API.customerOrders.startProduction(id), { rowVer }),
  stop: (id: number, reason: string, rowVer: string) =>
    post<unknown>(API.customerOrders.stop(id), { reason, rowVer }),
};
