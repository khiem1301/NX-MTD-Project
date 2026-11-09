import type { MaterialOption } from '@/types/lookup';

// Khớp DTO BE (Application/DTOs/*). Không tự đổi tên field.

export type PageParams = { page: number; pageSize: number; keyword?: string };

export type Material = {
  materialId: number;
  materialCode: string;
  materialName: string;
  materialType: string;
  category: string;
  unit: string;
  /** Màu NVL (26/10); '' = không phân màu. Cùng loại vải khác màu là hai NVL. */
  color: string;
  /** Khổ vải, cm (chỉ vải); dùng ước tính vải cắt bù. */
  widthCm?: number | null;
  status: string;
  createdAt: string;
};

export type MaterialQuery = PageParams & { materialType?: string; status?: string };

export type SaveMaterialPayload = {
  materialCode?: string;
  materialName: string;
  materialType: string;
  category: string;
  unit: string;
  color?: string;
  widthCm?: number | null;
};

export type OrderMaterial = {
  orderMaterialId: number;
  orderId: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  materialType: string;
  unit: string;
  materialColor: string;
  requiredQty: number;
  customerQty: number;
  /** Số lần khách dự kiến giao (bắt buộc khi khách cấp); receivedCount = số phiếu nhập đã xác nhận. */
  expectedDeliveryCount: number | null;
  /** Ngày khách dự kiến giao (yyyy-MM-dd), có khi khách cấp. */
  expectedDeliveryDate?: string | null;
  receivedCount: number;
  purchaseQty: number | null;
  source: string | null;
  note: string | null;
  receivedQty: number;
  /** Khổ vải, cm (chỉ vải). */
  materialWidthCm?: number | null;
};

/** Mã hàng trong đơn và các NVL định mức của những màu × size đơn đặt. */
export type OrderProductMaterials = {
  productId: number;
  productCode: string;
  productName: string;
  materials: MaterialOption[];
};

/** Thêm nhiều NVL cùng lúc: một dòng lỗi thì không lưu dòng nào. */
export type CreateOrderMaterialsPayload = {
  orderId: number;
  lines: Omit<SaveOrderMaterialPayload, 'orderId'>[];
};

/** NVL đã gồm màu (26/10) nên không nhập màu riêng. */
export type SaveOrderMaterialPayload = {
  orderId?: number;
  materialId: number;
  requiredQty: number;
  customerQty: number;
  expectedDeliveryCount?: number | null;
  expectedDeliveryDate?: string | null;
  note?: string;
};

export type MaterialReceiptListItem = {
  materialReceiptId: number;
  receiptCode: string;
  receiptType: string;
  orderId: number | null;
  orderCode: string | null;
  source: string;
  purchaseOrderId: number | null;
  poCode: string | null;
  receiptDate: string;
  status: string;
  submittedAt: string;
  confirmedAt: string | null;
  confirmedByName: string | null;
  lineCount: number;
};

export type MaterialReceiptLine = {
  materialReceiptLineId: number;
  orderMaterialId: number | null;
  purchaseOrderLineId: number | null;
  originalReceiptLineId: number | null;
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  unitPrice: number | null;
  /** Phiếu thường: còn thiếu sau phiếu này; phiếu bổ sung: số Quản đốc chọn giao bù. */
  toReplenishQty: number | null;
  acceptedQty: number;
  /** Phiếu này là lần nhận thứ mấy của NVL / số lần giao dự kiến. */
  deliveryNo: number | null;
  expectedDeliveryCount: number | null;
};

export type MaterialReceipt = Omit<MaterialReceiptListItem, 'lineCount'> & {
  originalReceiptId: number | null;
  originalReceiptCode: string | null;
  shortageRequestId: number | null;
  shortDeliveryRequestId: number | null;
  returnReason: string | null;
  note: string | null;
  lines: MaterialReceiptLine[];
};

export type PurchaseOrderListItem = {
  purchaseOrderId: number;
  poCode: string;
  orderId: number | null;
  orderCode: string | null;
  supplierName: string | null;
  goodsStatus: string;
  paymentStatus: string;
  createdAt: string;
  lineCount: number;
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
  receivedQty: number;
  notReplenishedQty: number;
};

export type PurchaseOrder = Omit<PurchaseOrderListItem, 'lineCount'> & {
  supplierId: number | null;
  note: string | null;
  lines: PurchaseOrderLine[];
};

export type PurchaseOrderQuery = PageParams & {
  goodsStatus?: string;
  stockOnly?: boolean;
  /** Chỉ đơn đã đặt và còn phải nhận (Thủ kho chọn khi lập phiếu nhập từ NCC). */
  receivable?: boolean;
};

/** orderId để trống = đơn mua dự trữ, không cần Admin tạo mã đơn hàng. */
export type CreatePurchaseOrderPayload = {
  orderId?: number;
  supplierId: number;
  /** Ngày đặt hàng (yyyy-MM-dd), không sau hôm nay. */
  orderDate: string;
  /** Ngày nhà cung cấp dự kiến giao (yyyy-MM-dd), không trước ngày đặt, không sau hạn giao của đơn hàng. */
  expectedDeliveryDate: string;
  /** Số lần nhà cung cấp dự kiến giao, từ 1 đến 99. */
  expectedDeliveryCount: number;
  note?: string;
  lines: {
    /** Chọn NVL có sẵn (đã gồm màu); hoặc nhập NVL mới bằng newMaterialName + newMaterialUnit + newMaterialColor. */
    materialId?: number;
    newMaterialName?: string;
    newMaterialUnit?: string;
    newMaterialColor?: string;
    orderedQty: number;
    unitPrice: number;
  }[];
};

export type MaterialReceiptQuery = PageParams & {
  status?: string;
  orderId?: number;
  purchaseOrderId?: number;
  source?: string;
};

/** Khách cấp: orderId + dòng chọn NVL của đơn (orderMaterialId). NCC: purchaseOrderId + dòng chọn dòng đơn mua. */
export type SaveReceiptPayload = {
  orderId?: number;
  purchaseOrderId?: number;
  source: string;
  receiptDate: string;
  note?: string;
  lines: {
    orderMaterialId?: number;
    purchaseOrderLineId?: number;
    acceptedQty: number;
  }[];
};

export type SaveSupplementaryPayload = {
  shortageRequestId: number;
  receiptDate: string;
  note?: string;
  lines: {
    originalReceiptLineId: number;
    acceptedQty: number;
  }[];
};

export type ShortDeliveryListItem = {
  requestId: number;
  requestCode: string;
  status: string;
  resolution: string | null;
  receiptId: number;
  receiptCode: string;
  /** Nguồn của phiếu nhập: CUSTOMER (khách cấp) hoặc SUPPLIER (mua NCC). */
  source: string;
  poCode: string | null;
  orderCode: string | null;
  createdAt: string;
};

export type ShortDeliveryLine = {
  requestLineId: number;
  materialReceiptLineId: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  shortfallQty: number;
  replenishQty: number | null;
  replenishedQty: number;
};

export type ShortDelivery = ShortDeliveryListItem & {
  resultNote: string | null;
  source: string;
  lines: ShortDeliveryLine[];
};

export type ResolveShortDeliveryPayload = {
  resolution: 'REPLENISH' | 'NO_REPLENISH';
  note?: string;
  lines?: { requestLineId: number; replenishQty: number }[];
};

export type ShortDeliveryQuery = PageParams & { awaitingReplenish?: boolean };

export type MaterialIssueListItem = {
  materialIssueId: number;
  issueCode: string;
  warehouseType: string;
  orderId: number;
  orderCode: string;
  workOrderId: number | null;
  workOrderCode: string | null;
  requestId: number | null;
  receivingTeamId: number;
  receivingTeamName: string;
  issueDate: string;
  status: string;
  issuedByName: string;
  receivedAt: string | null;
  lineCount: number;
};

export type MaterialIssueLine = {
  materialIssueLineId: number;
  materialId: number | null;
  materialCode: string | null;
  materialName: string | null;
  unit: string | null;
  materialColor: string | null;
  orderLineId: number | null;
  /** Chỉ dòng cấp BTP: null = bộ đủ; có giá trị = chi tiết lẻ (hàng cắt bù). */
  productComponentMaterialId: number | null;
  componentName: string | null;
  productCode: string | null;
  color: string | null;
  sizeCode: string | null;
  issuedQty: number;
  confirmedQty: number | null;
  discrepancyReason: string | null;
};

export type MaterialIssue = Omit<MaterialIssueListItem, 'lineCount'> & {
  receivedByName: string | null;
  rejectReason: string | null;
  createdAt: string;
  lines: MaterialIssueLine[];
};

export type MaterialIssueQuery = PageParams & {
  status?: string;
  warehouseType?: string;
  workOrderId?: number;
};

export type CreateIssuePayload = {
  warehouseType: string;
  workOrderId?: number;
  orderId?: number;
  receivingTeamId?: number;
  requestId?: number;
  issueDate: string;
  lines: {
    materialId?: number;
    orderLineId?: number;
    productComponentMaterialId?: number | null;
    issuedQty: number;
  }[];
};

export type ConfirmIssuePayload = {
  lines: { materialIssueLineId: number; confirmedQty: number; discrepancyReason?: string }[];
};

export type StockBalance = {
  warehouseType: string;
  materialId: number | null;
  materialCode: string | null;
  materialName: string | null;
  materialType: string | null;
  unit: string | null;
  materialColor: string | null;
  orderLineId: number | null;
  /** Chỉ kho BTP: null = bộ đủ; có giá trị = chi tiết lẻ (hàng cắt bù). */
  productComponentMaterialId: number | null;
  componentName: string | null;
  orderId: number | null;
  orderCode: string | null;
  productCode: string | null;
  productName: string | null;
  color: string | null;
  sizeCode: string | null;
  onHandQty: number;
  reservedQty: number;
  availableQty: number;
};

export type MaterialOrderScope = 'NO_ORDER';

export type StockBalanceQuery = PageParams & {
  warehouseType: string;
  orderId?: number;
  /** Chỉ kho NVL: NVL không thuộc đơn hàng nào (mua dự trữ dùng chung). */
  orderScope?: MaterialOrderScope;
  color?: string;
  sizeCode?: string;
};

export type StockCardQuery = {
  warehouseType: string;
  materialId?: number;
  orderLineId?: number;
  productComponentMaterialId?: number | null;
};

export type StockCardEntry = {
  stockTransactionId: number;
  transactionDate: string;
  createdAt: string;
  sourceDocType: string;
  sourceDocId: number;
  sourceDocCode: string | null;
  qtyChange: number;
  balanceAfter: number;
};

/** Báo cáo nhập xuất tồn tổng hợp theo đơn + kỳ. */
export type NxtReportQuery = PageParams & {
  warehouseType: string;
  orderId: number;
  fromDate: string;
  toDate: string;
};

export type NxtSummary = {
  warehouseType: string;
  materialId: number | null;
  materialCode: string | null;
  materialName: string | null;
  materialType: string | null;
  unit: string | null;
  materialColor: string | null;
  orderLineId: number | null;
  orderId: number | null;
  orderCode: string | null;
  productCode: string | null;
  productName: string | null;
  color: string | null;
  sizeCode: string | null;
  productComponentMaterialId: number | null;
  componentName: string | null;
  openingQty: number;
  inQty: number;
  outQty: number;
  closingQty: number;
};

/** BTP tổ may theo dòng đơn (26/10): số bộ đã nhận từ kho, đã QC đạt, hàng hủy, còn trên chuyền. */
export type TeamPieceHolding = {
  sewingTeamId: number;
  teamName: string;
  orderLineId: number;
  orderCode: string;
  productCode: string;
  color: string;
  sizeCode: string;
  receivedQty: number;
  finishedQty: number;
  scrapQty: number;
  onLineQty: number;
};

export const ORDER_MATERIAL_SUPPLY = { Customer: 'Customer', Purchase: 'Purchase' } as const;
export type OrderMaterialSupply =
  (typeof ORDER_MATERIAL_SUPPLY)[keyof typeof ORDER_MATERIAL_SUPPLY];

export const ORDER_MATERIAL_RECEIPT_STATE = { Pending: 'Pending', Done: 'Done' } as const;
export type OrderMaterialReceiptState =
  (typeof ORDER_MATERIAL_RECEIPT_STATE)[keyof typeof ORDER_MATERIAL_RECEIPT_STATE];

export type OrderMaterialPageQuery = {
  orderId: number;
  page: number;
  pageSize: number;
  keyword?: string;
  supply?: OrderMaterialSupply;
  receiptState?: OrderMaterialReceiptState;
};

/** NVL khách sẽ cấp mà kho chưa nhận đủ. */
export type IncomingCustomerMaterial = {
  orderMaterialId: number;
  orderId: number;
  orderCode: string;
  materialCode: string;
  materialName: string;
  materialColor: string;
  unit: string;
  customerQty: number;
  receivedQty: number;
  outstandingQty: number;
  /** Số lần khách dự kiến giao và số lần kho đã nhận (phiếu nhập đã xác nhận). */
  expectedDeliveryCount: number | null;
  expectedDeliveryDate?: string | null;
  receivedCount: number;
};

/** Dòng đơn mua đã đặt hàng mà kho chưa nhận đủ. */
export type IncomingSupplierMaterial = {
  purchaseOrderId: number;
  poCode: string;
  orderId: number | null;
  orderCode: string | null;
  supplierName: string | null;
  expectedDeliveryCount: number | null;
  expectedDeliveryDate?: string | null;
  receivedCount: number;
  purchaseOrderLineId: number;
  materialCode: string;
  materialName: string;
  materialColor: string;
  unit: string;
  orderedQty: number;
  receivedQty: number;
  outstandingQty: number;
  goodsStatus: string;
};

/** Dòng đơn mua đã đóng vì giao thiếu: phần thiếu đã chốt không bù, không nhận tiếp được. */
export type ClosedShortPurchase = {
  purchaseOrderId: number;
  poCode: string;
  orderId: number | null;
  orderCode: string | null;
  supplierName: string | null;
  purchaseOrderLineId: number;
  materialCode: string;
  materialName: string;
  materialColor: string;
  unit: string;
  orderedQty: number;
  receivedQty: number;
  notReplenishedQty: number;
};

export type IncomingMaterials = {
  customer: IncomingCustomerMaterial[];
  supplier: IncomingSupplierMaterial[];
  closedShort: ClosedShortPurchase[];
};

/** Điền sẵn phiếu nhập từ màn "NVL chờ nhận": nguồn, đơn / đơn mua và dòng cần nhận với số còn phải nhận. */
export type ReceiptPreset = {
  source: string;
  orderId?: number;
  purchaseOrderId?: number;
  orderMaterialId?: number;
  purchaseOrderLineId?: number;
  acceptedQty: number;
  /** Nhận cả đơn mua: mỗi dòng NVL còn phải nhận một dòng phiếu (thay cho purchaseOrderLineId / acceptedQty). */
  purchaseLines?: { purchaseOrderLineId: number; acceptedQty: number }[];
};

/** Dòng (màu - size) của lệnh cắt, để chọn khi đề xuất số vải cần cấp. */
export type WorkOrderLineInfo = {
  productCode: string;
  lines: { color: string; sizeCode: string }[];
};

/** Số mét vải một NVL cần cho các dòng đã chọn của lệnh cắt (GET .../fabric-needs). */
export type FabricNeed = { materialId: number; needMeters: number };
