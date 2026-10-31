import { API } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  ConfirmPaymentPayload,
  PlacePayload,
  PurchaseOrderDetail,
  PurchaseOrderListItem,
  PurchaseOrderQuery,
  PurchaseRole,
  ReasonPayload,
  RepricePayload,
  SaveSupplierPayload,
  Supplier,
  SupplierDetail,
  SupplierOption,
  SupplierQuery,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

const listUrl = (role: PurchaseRole) =>
  role === 'manager' ? API.purchase.orders : API.purchase.payments;
const detailUrl = (role: PurchaseRole, id: number) =>
  role === 'manager' ? API.purchase.order(id) : API.purchase.payment(id);

export const purchaseApi = {
  // ---------- Nhà cung cấp (Admin) ----------
  listSuppliers: (query: SupplierQuery) => get<Paginated<Supplier>>(API.purchase.suppliers, query),
  getSupplier: (id: number) => get<SupplierDetail>(API.purchase.supplier(id)),
  createSupplier: (body: SaveSupplierPayload) => post<number>(API.purchase.suppliers, body),
  updateSupplier: (id: number, body: SaveSupplierPayload) =>
    put<void>(API.purchase.supplier(id), body),
  discontinueSupplier: (id: number) => post<void>(API.purchase.supplierDiscontinue(id)),
  reactivateSupplier: (id: number) => post<void>(API.purchase.supplierReactivate(id)),

  /** Quản đốc: chỉ nhà cung cấp đang hợp tác. */
  supplierOptions: () => get<SupplierOption[]>(API.purchase.supplierOptions),

  // ---------- Đơn mua ----------
  listOrders: (role: PurchaseRole, query: PurchaseOrderQuery) =>
    get<Paginated<PurchaseOrderListItem>>(listUrl(role), query),
  getOrder: (role: PurchaseRole, id: number) => get<PurchaseOrderDetail>(detailUrl(role, id)),

  // Quản đốc
  place: (id: number, body: PlacePayload) => post<void>(API.purchase.place(id), body),
  cancel: (id: number, body: ReasonPayload) => post<void>(API.purchase.cancel(id), body),
  reprice: (id: number, body: RepricePayload) => post<void>(API.purchase.reprice(id), body),

  // Kế toán
  confirmPayment: (id: number, body: ConfirmPaymentPayload) =>
    post<void>(API.purchase.confirmPayment(id), body),
  returnPayment: (id: number, body: ReasonPayload) =>
    post<void>(API.purchase.returnPayment(id), body),
};
