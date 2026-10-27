import type { OrderStatus, OrderMaterialStatus } from '@/constants/orderStatus';
import type { AuditEntry } from '@/types/attachment';

export type Customer = {
  customerId: number;
  customerName: string;
  phone: string | null;
  email: string | null;
  defaultAddress: string | null;
};
export type SaveCustomerPayload = {
  customerName: string;
  phone?: string | null;
  email?: string | null;
  defaultAddress?: string | null;
};

export type ProductStatus = 'Active' | 'Discontinued';
export type ProductListItem = {
  productId: number;
  productCode: string;
  productName: string;
  sampleImageUrl: string | null;
  status: ProductStatus;
  isUsed: boolean;
  createdAt: string;
};
export type ProductDetail = ProductListItem & {
  description: string | null;
  lastSizeRatio: string | null;
  history: AuditEntry[];
};
export type SaveProductPayload = {
  productCode: string;
  productName: string;
  sampleImageUrl?: string | null;
  description?: string | null;
};
/** Định mức theo mã hàng (UC10): thành phần, biến thể màu × size và NVL của từng biến thể. */
export type ProductComponentItem = {
  productComponentId: number;
  componentName: string;
  componentType: 'Panel' | 'Trim';
  piecesPerGarment: number;
  sortOrder: number;
  status: string;
};
export type ProductVariantItem = { productVariantId: number; color: string; sizeCode: string; status: string };
export type ProductNormItem = {
  productComponentMaterialId: number;
  componentName: string;
  color: string;
  sizeCode: string;
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  qtyPerGarment: number;
  wastagePercent: number;
  lengthCm: number | null;
  widthCm: number | null;
  specNote: string | null;
};
export type ProductSpec = {
  productId: number;
  components: ProductComponentItem[];
  variants: ProductVariantItem[];
  norms: ProductNormItem[];
};
export type SaveProductSpecPayload = {
  components: {
    productComponentId: number | null;
    componentName: string;
    componentType: 'Panel' | 'Trim';
    piecesPerGarment: number;
    sortOrder: number;
  }[];
  variants: { productVariantId: number | null; color: string; sizeCode: string }[];
  norms: {
    componentName: string;
    color: string;
    sizeCode: string;
    materialId: number;
    materialColor: string | null;
    qtyPerGarment: number;
    wastagePercent: number;
    lengthCm: number | null;
    widthCm: number | null;
    specNote: string | null;
  }[];
};
export type ProductQuery = { page: number; pageSize: number; keyword?: string; status?: ProductStatus };

export type OrderListItem = {
  orderId: number;
  orderCode: string;
  customerName: string;
  receivedDate: string;
  dueDate: string;
  status: OrderStatus;
  materialStatus: OrderMaterialStatus;
  isDelayRisk: boolean;
  totalQty: number;
  totalAmount: number;
  productCodes: string;
};

export type OrderLine = {
  orderLineId: number;
  productId: number;
  productCode: string;
  productName: string;
  color: string;
  sizeCode: string;
  orderedQty: number;
  cmtUnitPrice: number;
};

export type OrderDetail = {
  orderId: number;
  orderCode: string;
  customerId: number;
  customerName: string;
  receivedDate: string;
  dueDate: string;
  status: OrderStatus;
  materialStatus: OrderMaterialStatus;
  isDelayRisk: boolean;
  stopReason: string | null;
  note: string | null;
  createdAt: string;
  updatedAt: string | null;
  /** byte[] → base64; gửi nguyên chuỗi khi sửa / hủy / dừng. */
  rowVer: string;
  lines: OrderLine[];
  history: AuditEntry[];
};

export type OrderLineInput = {
  orderLineId: number | null;
  productId: number;
  color: string;
  sizeCode: string;
  orderedQty: number;
  cmtUnitPrice: number;
};

export type CreateOrderPayload = {
  orderCode: string;
  customerId: number;
  receivedDate: string;
  dueDate: string;
  note?: string | null;
  lines: OrderLineInput[];
};
export type UpdateOrderPayload = CreateOrderPayload & { rowVer: string };

export type OrderQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  status?: OrderStatus;
  customerId?: number;
  dueFrom?: string;
  dueTo?: string;
};
