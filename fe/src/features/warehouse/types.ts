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

export type OrderMaterialNorm = {
  orderMaterialNormId: number;
  productId: number;
  productCode: string;
  componentName: string;
  garmentColor: string | null;
  sizeCode: string | null;
  qtyPerPiece: number;
  wastagePercent: number;
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
  customerExpectedDate: string | null;
  purchaseQty: number | null;
  source: string | null;
  note: string | null;
  normRequiredQty: number;
  receivedQty: number;
  norms: OrderMaterialNorm[];
};

export type SaveNormPayload = {
  productId: number;
  componentName?: string;
  garmentColor?: string;
  sizeCode?: string;
  qtyPerPiece: number;
  wastagePercent: number;
};

export type SaveOrderMaterialPayload = {
  orderId?: number;
  materialId: number;
  materialColor?: string;
  requiredQty: number;
  customerQty: number;
  customerExpectedDate?: string | null;
  note?: string;
  norms: SaveNormPayload[];
};

export type MaterialReceiptListItem = {
  materialReceiptId: number;
  receiptCode: string;
  receiptType: string;
  orderId: number | null;
  orderCode: string | null;
  source: string;
  receiptDate: string;
  status: string;
  submittedAt: string;
  confirmedAt: string | null;
  confirmedByName: string | null;
  lineCount: number;
};

export type MaterialReceiptLine = {
  materialReceiptLineId: number;
  orderMaterialId: number;
  materialId: number;
  materialCode: string;
  materialName: string;
  unit: string;
  materialColor: string;
  acceptedQty: number;
  shortQty: number;
  defectiveQty: number;
};

export type MaterialReceipt = Omit<MaterialReceiptListItem, 'lineCount'> & {
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
};

export type PurchaseOrder = Omit<PurchaseOrderListItem, 'lineCount'> & {
  supplierId: number | null;
  note: string | null;
  lines: PurchaseOrderLine[];
};

export type PurchaseOrderQuery = PageParams & { goodsStatus?: string; stockOnly?: boolean };

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

export type MaterialReceiptQuery = PageParams & { status?: string; orderId?: number };

export type SaveReceiptPayload = {
  orderId: number;
  source: string;
  receiptDate: string;
  note?: string;
  lines: { orderMaterialId: number; acceptedQty: number; shortQty: number; defectiveQty: number }[];
};

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
  lines: { materialId?: number; materialColor?: string; orderLineId?: number; issuedQty: number }[];
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
