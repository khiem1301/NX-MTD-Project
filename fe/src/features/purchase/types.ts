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
  discontinueReason: string | null;
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

export type SupplierQuery = {
  page: number;
  pageSize: number;
  keyword?: string;
  status?: SupplierStatus;
};

// ---------- Đơn mua: đặt hàng (Quản đốc) và thanh toán (Kế toán) ----------

export type PoGoodsStatus =
  | 'PendingFinalisation'
  | 'Ordered'
  | 'PartiallyReceived'
  | 'Received'
  | 'ClosedShort'
  | 'CustomerSupplied'
  | 'Rejected';

export type PoPaymentStatus = 'Unpaid' | 'Paid' | 'PartiallyPaid' | 'Returned';

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
  /** Số đã nhận (phiếu nhập đã xác nhận) và số Quản đốc chốt không bù. */
  receivedQty: number;
  notReplenishedQty: number;
};

/** Nút nào hiện được: do server tính theo vai trò + trạng thái đơn. */
export type PurchaseOrderPermissions = {
  canPlace: boolean;
  canCancel: boolean;
  canReprice: boolean;
  canConfirmPayment: boolean;
  canReturnPayment: boolean;
  /** DEV-ALL-185: nhập / sửa giá trị thực tế (đơn Chốt không bù; một lần, sửa trong 24 giờ kể từ lần đầu). */
  canSetActualAmount: boolean;
  /** DEV-ALL-186: Quản đốc sửa đơn trong 24 giờ kể từ lúc tạo (chưa nhận hàng, chưa thanh toán). */
  canEditOrder: boolean;
};

/** Một lần thanh toán đơn mua; canEdit: Kế toán, trong 24 giờ kể từ lúc nhập. Không xóa được. */
export type PurchasePayment = {
  purchasePaymentId: number;
  paymentDate: string;
  amount: number;
  paymentMethod: PaymentMethod;
  paymentNote: string | null;
  createdAt: string;
  updatedAt: string | null;
  editReason: string | null;
  canEdit: boolean;
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
  /** Số lần NCC dự kiến giao; receivedCount = số phiếu nhập thường đã xác nhận (phiếu bổ sung không đếm). */
  expectedDeliveryCount: number | null;
  expectedDeliveryDate?: string | null;
  /** Giao từ 2 lần trở lên: ngày giao đến (yyyy-MM-dd). */
  expectedDeliveryDateTo?: string | null;
  expectedDeliveryDates?: string[];
  receivedCount: number;
  note: string | null;
  rejectReason: string | null;
  paymentReturnReason: string | null;
  paymentDate: string | null;
  paidAmount: number | null;
  paymentMethod: PaymentMethod | null;
  paymentNote: string | null;
  totalAmount: number;
  /** DEV-ALL-129: tổng giá trị thực tế đơn Chốt không bù (Kế toán nhập); receivedAmount = Σ số đã nhận × đơn giá (gợi ý). */
  actualAmount: number | null;
  /** DEV-ALL-185: lúc Kế toán nhập giá trị thực tế lần đầu; null = chưa nhập (số tự tính theo số đã nhận không tính là đã nhập). */
  actualAmountSetAt: string | null;
  receivedAmount: number;
  payments: PurchasePayment[];
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
  expectedDeliveryCount: number;
  /** Mỗi lần giao một ngày (yyyy-MM-dd), đúng số lần giao. */
  expectedDeliveryDates: string[];
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

/** DEV-ALL-185: giá trị thực tế của đơn Chốt không bù; reason bắt buộc khi sửa (đã nhập lần đầu). */
export type SetActualAmountPayload = { amount: number; reason?: string; rowVer: string };

export type UpdatePaymentPayload = {
  paymentDate: string;
  amount: number;
  paymentMethod: PaymentMethod;
  paymentNote: string | null;
  reason: string;
  rowVer: string;
};

export type ReasonPayload = { reason: string; rowVer: string };

/** DEV-ALL-186: sửa đơn mua trong 24 giờ; reason bắt buộc. */
export type EditOrderPayload = {
  orderDate: string;
  expectedDeliveryCount: number;
  expectedDeliveryDates: string[];
  note: string | null;
  reason: string;
  lines: { purchaseOrderLineId: number; orderedQty: number; unitPrice: number }[];
};
