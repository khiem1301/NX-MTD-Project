import type { AuditEntry } from '@/types/attachment';

// ---------- Nhà cung cấp (UC14) ----------

export type SupplierStatus = 'Active' | 'Discontinued';

export type Supplier = {
  supplierId: number;
  supplierName: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  materialTypes: string | null;
  note: string | null;
  status: SupplierStatus;
  createdAt: string;
  purchaseOrderCount: number;
};

export type SupplierDetail = { supplier: Supplier; history: AuditEntry[] };

export type SaveSupplierPayload = {
  supplierName: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  materialTypes: string | null;
  note: string | null;
};

/** Ô chọn nhà cung cấp khi đặt hàng: BE chỉ trả nhà cung cấp đang hợp tác. */
export type SupplierOption = { supplierId: number; supplierName: string };

export type SupplierQuery = { page: number; pageSize: number; keyword?: string; status?: SupplierStatus };

// ---------- Đơn mua: đặt hàng (Quản đốc) và thanh toán (Kế toán) ----------

export type PoGoodsStatus =
  | 'PendingFinalisation'
  | 'Ordered'
  | 'PartiallyReceived'
  | 'Received'
  | 'ClosedShort'
  | 'CustomerSupplied'
  | 'Rejected';

export type PoPaymentStatus = 'Unpaid' | 'Paid' | 'Returned';

export type PaymentMethod = 'BankTransfer' | 'Cash';

/** Quản đốc xem mọi đơn mua; Kế toán chỉ thấy đơn đã đặt hàng (server quyết định, FE chỉ chọn route). */
export type PurchaseRole = 'manager' | 'accountant';

export type PurchaseOrderListItem = {
  purchaseOrderId: number;
  poCode: string;
  orderCode: string | null;
  supplierName: string | null;
  goodsStatus: PoGoodsStatus;
  paymentStatus: PoPaymentStatus;
  orderDate: string | null;
  totalAmount: number;
  paidAmount: number | null;
  createdAt: string;
};

export type PurchaseOrderLine = {
  purchaseOrderLineId: number;
  orderMaterialId: number | null;
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  orderedQty: number;
  unitPrice: number;
  amount: number | null;
};

/** Nút nào hiện được: do server tính theo vai trò + trạng thái đơn. */
export type PurchaseOrderPermissions = {
  canPlace: boolean;
  canCancel: boolean;
  canReprice: boolean;
  canConfirmPayment: boolean;
  canReturnPayment: boolean;
};

export type PurchaseOrderDetail = {
  purchaseOrderId: number;
  poCode: string;
  orderId: number | null;
  orderCode: string | null;
  supplierId: number | null;
  supplierName: string | null;
  goodsStatus: PoGoodsStatus;
  paymentStatus: PoPaymentStatus;
  orderDate: string | null;
  expectedDeliveryDate: string | null;
  note: string | null;
  rejectReason: string | null;
  paymentReturnReason: string | null;
  paymentDate: string | null;
  paidAmount: number | null;
  paymentMethod: PaymentMethod | null;
  paymentNote: string | null;
  paidAt: string | null;
  totalAmount: number;
  createdAt: string;
  rowVer: string;
  lines: PurchaseOrderLine[];
  permissions: PurchaseOrderPermissions;
  history: AuditEntry[];
};

export type PurchaseOrderQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  goodsStatus?: PoGoodsStatus;
  paymentStatus?: PoPaymentStatus;
};

export type PlacePayload = {
  supplierId: number;
  orderDate: string;
  expectedDeliveryDate: string | null;
  note: string | null;
  lines: { purchaseOrderLineId: number; orderedQty: number; unitPrice: number }[];
  rowVer: string;
};

export type RepricePayload = {
  note: string | null;
  lines: { purchaseOrderLineId: number; unitPrice: number }[];
  rowVer: string;
};

export type ConfirmPaymentPayload = {
  paymentDate: string;
  paidAmount: number;
  paymentMethod: PaymentMethod;
  paymentNote: string | null;
  rowVer: string;
};

export type ReasonPayload = { reason: string; rowVer: string };
