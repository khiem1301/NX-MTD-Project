// Khớp DTO BE (Application/DTOs/*). Không tự đổi tên field.

export type PageParams = { page: number; pageSize: number; keyword?: string };

export type Material = {
  materialId: number;
  materialCode: string;
  materialName: string;
  materialType: string;
  category: string;
  unit: string;
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
  receivedCount: number;
  purchaseQty: number | null;
  source: string | null;
  note: string | null;
  /** Số cần theo định mức của mã hàng × số đặt (chỉ đọc). */
  normRequiredQty: number;
  receivedQty: number;
};

export type SaveOrderMaterialPayload = {
  orderId?: number;
  materialId: number;
  materialColor?: string;
  requiredQty: number;
  customerQty: number;
  expectedDeliveryCount?: number | null;
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
  toReplenishQty: number | null;
  acceptedQty: number;
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
  note?: string;
  lines: {
    /** Chọn NVL có sẵn; hoặc nhập NVL mới bằng newMaterialName + newMaterialUnit. */
    materialId?: number;
    newMaterialName?: string;
    newMaterialUnit?: string;
    materialColor?: string;
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
  productComponentId: number | null;
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
  lines: { materialId?: number; materialColor?: string; orderLineId?: number; productComponentId?: number | null; issuedQty: number }[];
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
  productComponentId: number | null;
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

export type StockBalanceQuery = PageParams & {
  warehouseType: string;
  orderId?: number;
  color?: string;
  sizeCode?: string;
};

export type StockCardQuery = {
  warehouseType: string;
  materialId?: number;
  materialColor?: string;
  orderLineId?: number;
  productComponentId?: number | null;
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

export type TeamPieceHolding = {
  sewingTeamId: number;
  teamName: string;
  orderLineId: number;
  orderCode: string;
  productCode: string;
  color: string;
  sizeCode: string;
  receivedFromWarehouse: number;
  handedToWorkers: number;
  retrievedFromWorkers: number;
  heldByTeam: number;
};

/** Đơn đang chạy mà NVL chưa về đủ (materialLines = 0: chưa ai khai báo "NVL của đơn"). */
export type PendingMaterialOrder = {
  orderId: number;
  orderCode: string;
  customerName: string;
  dueDate: string;
  status: string;
  materialStatus: string;
  materialLines: number;
  customerQty: number;
  toBuyQty: number;
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
  receivedCount: number;
  purchaseOrderLineId: number;
  materialCode: string;
  materialName: string;
  materialColor: string;
  unit: string;
  orderedQty: number;
  receivedQty: number;
  outstandingQty: number;
};

export type IncomingMaterials = { customer: IncomingCustomerMaterial[]; supplier: IncomingSupplierMaterial[] };

/** Điền sẵn phiếu nhập từ màn "NVL chờ nhận": nguồn, đơn / đơn mua và dòng cần nhận với số còn phải nhận. */
export type ReceiptPreset = {
  source: string;
  orderId?: number;
  purchaseOrderId?: number;
  orderMaterialId?: number;
  purchaseOrderLineId?: number;
  acceptedQty: number;
};
