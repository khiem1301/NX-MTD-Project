import { API, type ProductScope } from '@/constants/apiEndpoints';
import { apiClient } from '@/services/apiClient';
import type { OrderMaterialImportResult } from '../utils/importMessages';
import type { ApiResponse, Paginated } from '@/types/api';
import type {
  CreateOrderPayload,
  Customer,
  ExcelBundle,
  ExcelBundleCheck,
  MaterialExcelRow,
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
  OrderImportCheckBody,
  OrderImportPreview,
  OrderSaveResult,
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
const specImportRequest = async <T>(
  target: SpecImportTarget,
  file: File,
  preview: boolean,
  replace: boolean,
) => {
  const url =
    target.scope === 'admin'
      ? preview
        ? API.products.importPreview(target.productId)
        : API.products.import(target.productId)
      : preview
        ? API.orderMarkers.importPreview(target.orderId, target.productId)
        : API.orderMarkers.import(target.orderId, target.productId);
  const params = {
    ...(target.scope === 'admin'
      ? { orderId: target.orderId, parts: target.parts?.join(',') || undefined }
      : {}),
    replace: replace || undefined,
  };
  return (await apiClient.post<ApiResponse<T>>(url, asFormData(file), { params })).data;
};

/** DEV-ALL-118: xem trước dòng hàng của đơn từ Excel; form đơn tự điền rồi người dùng Lưu. */
export const orderImportApi = {
  preview: (file: File) =>
    post<OrderImportPreview>(API.customerOrders.importPreview, asFormData(file)),
  previewData: (body: OrderImportCheckBody) =>
    post<OrderImportPreview>(API.customerOrders.importPreviewData, body),
};

/** DEV-ALL-115: nhập Excel định mức mã hàng + bìa của đơn. */
/** DEV-ALL-122: popup kiểm tra Excel nhiều sheet — đọc file, kiểm bản đã sửa (không ghi), xem trước / nhập định mức từ JSON. */
/** Popup kiểm tra Excel gọi API Admin; Quản đốc (bìa cắt, NVL của đơn) gọi API Quản đốc cùng chức năng. */
export type ReviewScope = 'admin' | 'manager';

export const excelBundleApi = {
  parse: (file: File, scope: ReviewScope = 'admin') =>
    post<ExcelBundle>(
      scope === 'admin' ? API.products.excelParse : API.orderMarkers.excelParse,
      asFormData(file),
    ),
  check: (
    body: {
      productId: number | null;
      orderId: number | null;
      bundle: ExcelBundle;
      replace?: boolean;
      /** DEV-ALL-193: NVL mới đang chờ ghi ở NVL của đơn. */
      pendingMaterials?: MaterialExcelRow[];
    },
    scope: ReviewScope = 'admin',
  ) =>
    post<ExcelBundleCheck>(
      scope === 'admin' ? API.products.excelCheck : API.orderMarkers.excelCheck,
      body,
    ),
  /** Nhập định mức + bìa của đơn từ bảng đã sửa; Quản đốc bắt buộc có đơn. */
  importSpecScoped: async (
    scope: ReviewScope,
    productId: number,
    workbook: SpecWorkbook,
    orderId: number | undefined,
    parts: SpecImportPart[],
    replace = false,
  ) => {
    if (scope === 'admin')
      return excelBundleApi.importSpec(productId, workbook, orderId, parts, replace);
    return (
      await apiClient.post<ApiResponse<SpecImportResult>>(
        API.orderMarkers.importData(orderId ?? 0, productId),
        workbook,
        { params: { parts: parts.join(','), replace: replace || undefined } },
      )
    ).data;
  },
  previewSpec: async (
    productId: number,
    workbook: SpecWorkbook,
    orderId: number | undefined,
    parts: SpecImportPart[],
    replace = false,
  ) =>
    (
      await apiClient.post<ApiResponse<SpecImportPreview>>(
        API.products.importPreviewData(productId),
        workbook,
        { params: { orderId, parts: parts.join(','), replace: replace || undefined } },
      )
    ).data.data,
  /** Trả cả message (số dòng đã nhập). */
  importSpec: async (
    productId: number,
    workbook: SpecWorkbook,
    orderId: number | undefined,
    parts: SpecImportPart[],
    replace = false,
  ) =>
    (
      await apiClient.post<ApiResponse<SpecImportResult>>(
        API.products.importData(productId),
        workbook,
        {
          params: { orderId, parts: parts.join(','), replace: replace || undefined },
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
  importOrderMaterials: async (
    orderId: number,
    rows: OrderMaterialExcelRow[],
    replace = false,
    scope: ReviewScope = 'admin',
  ) =>
    (
      await apiClient.post<ApiResponse<OrderMaterialImportResult | null>>(
        `${API.warehouse.orderMaterials(scope)}/import-data`,
        rows,
        { params: { orderId, replace: replace || undefined } },
      )
    ).data,
};

export const specImportApi = {
  preview: async (target: SpecImportTarget, file: File, replace = false) =>
    (await specImportRequest<SpecImportPreview>(target, file, true, replace)).data,
  /** Trả cả message (số dòng đã nhập) để hiện cho người dùng. */
  import: (target: SpecImportTarget, file: File, replace = false) =>
    specImportRequest<SpecImportResult>(target, file, false, replace),
};

export const orderApi = {
  list: (q: OrderQuery) => get<Paginated<OrderListItem>>(API.customerOrders.base, q),
  nextCode: () => get<string>(API.customerOrders.nextCode),
  get: (id: number) => get<OrderDetail>(API.customerOrders.byId(id)),
  overviews: (q: OrderOverviewQuery) =>
    get<Paginated<OrderOverviewListItem>>(API.customerOrders.overviews, q),
  overview: (id: number) => get<OrderOverview>(API.customerOrders.overviewById(id)),
  create: (body: CreateOrderPayload) => post<OrderSaveResult>(API.customerOrders.base, body),
  update: (id: number, body: UpdateOrderPayload) =>
    put<OrderSaveResult>(API.customerOrders.byId(id), body),
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
  /** Định mức của mã hàng, Quản đốc chỉ đọc (API Admin của mã hàng không mở cho Quản đốc). */
  productSpec: (productId: number) => get<ProductSpec>(API.orderMarkers.productSpec(productId)),
  save: (orderId: number, productId: number, body: SaveOrderMarkersPayload) =>
    put<OrderMarkers>(API.orderMarkers.byProduct(orderId, productId), body),
};
