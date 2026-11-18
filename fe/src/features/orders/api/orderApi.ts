import { API, type ProductScope } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  CreateOrderPayload,
  Customer,
  ExcelBundle,
  ExcelBundleCheck,
  OrderMaterialCheck,
  OrderMaterialExcelRow,
  SpecImportPart,
  SpecWorkbook,
  CustomerOption,
  CustomerQuery,
  OrderDetail,
  OrderListItem,
  OrderOverview,
  OrderOverviewListItem,
  OrderOverviewQuery,
  OrderMarkers,
  OrderQuery,
  ProductDetail,
  ProductListItem,
  ProductQuery,
  ProductSpec,
  SaveCustomerPayload,
  SaveOrderMarkersPayload,
  SaveProductPayload,
  SaveProductSpecPayload,
  OrderImportPreview,
  ProductImportPreview,
  SpecImportPreview,
  SpecImportResult,
  SpecImportTarget,
  UpdateOrderPayload,
} from '../types';

const get = async <T>(url: string, params?: object): Promise<T> =>
  (await apiClient.get<ApiResponse<T>>(url, { params })).data.data;
const post = async <T>(url: string, body?: unknown): Promise<T> =>
  (await apiClient.post<ApiResponse<T>>(url, body)).data.data;
const put = async <T>(url: string, body: unknown): Promise<T> =>
  (await apiClient.put<ApiResponse<T>>(url, body)).data.data;

export const customerApi = {
  list: (query: CustomerQuery = {}) => get<Paginated<Customer>>(API.customers.base, query),
  options: () => get<CustomerOption[]>(API.customers.options),
  create: (body: SaveCustomerPayload) => post<number>(API.customers.base, body),
  update: (id: number, body: SaveCustomerPayload) => put<unknown>(API.customers.byId(id), body),
  discontinue: (id: number, reason: string | null) =>
    post<void>(API.customers.discontinue(id), { reason }),
  reactivate: (id: number) => post<void>(API.customers.reactivate(id)),
};

export const productApi = {
  uploadSampleImage: async (file: File): Promise<string> => {
    const body = new FormData();
    body.append('file', file);
    return post<string>(API.products.sampleImage(), body);
  },
  sampleImageBlob: async (key: string, scope: ProductScope = 'admin'): Promise<Blob> =>
    (
      await apiClient.get<Blob>(API.products.sampleImage(scope), {
        params: { key },
        responseType: 'blob',
      })
    ).data,
  list: (q: ProductQuery, scope: ProductScope = 'admin') =>
    get<Paginated<ProductListItem>>(API.products.base(scope), q),
  get: (id: number) => get<ProductDetail>(API.products.byId(id)),
  create: (body: SaveProductPayload) => post<number>(API.products.base(), body),
  update: (id: number, body: SaveProductPayload) => put<unknown>(API.products.byId(id), body),
  discontinue: (id: number) => post<unknown>(API.products.discontinue(id)),
  reactivate: (id: number) => post<unknown>(API.products.reactivate(id)),
  getSpec: (id: number, scope: ProductScope = 'admin') =>
    get<ProductSpec>(API.products.spec(id, scope)),
  saveSpec: (id: number, body: SaveProductSpecPayload, scope: ProductScope = 'admin') =>
    put<ProductSpec>(API.products.spec(id, scope), body),
};

const asFormData = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return form;
};

/** Màn Mã hàng gọi API Admin (kèm ?orderId= nếu chọn đơn); màn Đơn hàng gọi API Quản đốc theo đơn × mã hàng. */
const specImportRequest = async <T>(target: SpecImportTarget, file: File, preview: boolean) => {
  const url =
    target.scope === 'admin'
      ? preview
        ? API.products.importPreview(target.productId)
        : API.products.import(target.productId)
      : preview
        ? API.orderMarkers.importPreview(target.orderId, target.productId)
        : API.orderMarkers.import(target.orderId, target.productId);
  const params =
    target.scope === 'admin'
      ? { orderId: target.orderId, parts: target.parts?.join(',') || undefined }
      : undefined;
  return (await apiClient.post<ApiResponse<T>>(url, asFormData(file), { params })).data;
};

/** DEV-ALL-118: xem trước dòng hàng của đơn từ Excel; form đơn tự điền rồi người dùng Lưu. */
export const orderImportApi = {
  preview: (file: File) =>
    post<OrderImportPreview>(API.customerOrders.importPreview, asFormData(file)),
};

/** DEV-ALL-120: tạo mã hàng hàng loạt từ Excel (mã đã có bỏ qua). */
export const productImportApi = {
  preview: (file: File) =>
    post<ProductImportPreview>(API.products.productsImportPreview, asFormData(file)),
  /** Trả cả message (số mã đã tạo / bỏ qua). */
  import: async (file: File) =>
    (await apiClient.post<ApiResponse<unknown>>(API.products.productsImport, asFormData(file)))
      .data,
};

/** DEV-ALL-115: nhập Excel định mức mã hàng + bìa của đơn. */
/** DEV-ALL-122: popup kiểm tra Excel nhiều sheet — đọc file, kiểm bản đã sửa (không ghi), xem trước / nhập định mức từ JSON. */
export const excelBundleApi = {
  parse: (file: File) => post<ExcelBundle>(API.products.excelParse, asFormData(file)),
  check: (body: { productId: number | null; orderId: number | null; bundle: ExcelBundle }) =>
    post<ExcelBundleCheck>(API.products.excelCheck, body),
  previewSpec: async (
    productId: number,
    workbook: SpecWorkbook,
    orderId: number | undefined,
    parts: SpecImportPart[],
  ) =>
    (
      await apiClient.post<ApiResponse<SpecImportPreview>>(
        API.products.importPreviewData(productId),
        workbook,
        { params: { orderId, parts: parts.join(',') } },
      )
    ).data.data,
  /** Trả cả message (số dòng đã nhập). */
  importSpec: async (
    productId: number,
    workbook: SpecWorkbook,
    orderId: number | undefined,
    parts: SpecImportPart[],
  ) =>
    (
      await apiClient.post<ApiResponse<SpecImportResult>>(
        API.products.importData(productId),
        workbook,
        {
          params: { orderId, parts: parts.join(',') },
        },
      )
    ).data,
  /** NVL của đơn (API Admin) từ dòng đã sửa. */
  previewOrderMaterials: async (orderId: number, rows: OrderMaterialExcelRow[]) =>
    (
      await apiClient.post<ApiResponse<OrderMaterialCheck>>(
        `${API.warehouse.orderMaterials('admin')}/import/preview-data`,
        rows,
        { params: { orderId } },
      )
    ).data.data,
  importOrderMaterials: async (orderId: number, rows: OrderMaterialExcelRow[]) =>
    (
      await apiClient.post<ApiResponse<unknown>>(
        `${API.warehouse.orderMaterials('admin')}/import-data`,
        rows,
        { params: { orderId } },
      )
    ).data,
};

export const specImportApi = {
  preview: async (target: SpecImportTarget, file: File) =>
    (await specImportRequest<SpecImportPreview>(target, file, true)).data,
  /** Trả cả message (số dòng đã nhập) để hiện cho người dùng. */
  import: (target: SpecImportTarget, file: File) =>
    specImportRequest<SpecImportResult>(target, file, false),
};

export const orderApi = {
  list: (q: OrderQuery) => get<Paginated<OrderListItem>>(API.customerOrders.base, q),
  nextCode: () => get<string>(API.customerOrders.nextCode),
  get: (id: number) => get<OrderDetail>(API.customerOrders.byId(id)),
  overviews: (q: OrderOverviewQuery) =>
    get<Paginated<OrderOverviewListItem>>(API.customerOrders.overviews, q),
  overview: (id: number) => get<OrderOverview>(API.customerOrders.overviewById(id)),
  create: (body: CreateOrderPayload) => post<number>(API.customerOrders.base, body),
  update: (id: number, body: UpdateOrderPayload) => put<unknown>(API.customerOrders.byId(id), body),
  cancel: (id: number, reason: string | null, rowVer: string) =>
    post<unknown>(API.customerOrders.cancel(id), { reason, rowVer }),
  startProduction: (id: number, rowVer: string) =>
    post<unknown>(API.customerOrders.startProduction(id), { rowVer }),
  stop: (id: number, reason: string, rowVer: string) =>
    post<unknown>(API.customerOrders.stop(id), { reason, rowVer }),
};

/** Bìa cắt của đơn (DEV-ALL-85). */
export const orderMarkerApi = {
  get: (orderId: number) => get<OrderMarkers>(API.orderMarkers.byOrder(orderId)),
  save: (orderId: number, productId: number, body: SaveOrderMarkersPayload) =>
    put<OrderMarkers>(API.orderMarkers.byProduct(orderId, productId), body),
};
