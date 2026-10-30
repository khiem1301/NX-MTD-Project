import type { StatusLabel } from '@/components/common/StatusTag';

// Giá trị khớp CHECK constraint DB / hằng số BE (Domain/Constants/WarehouseConstants.cs).
export const WAREHOUSE_TYPE = {
  MATERIAL: 'MATERIAL',
  SEMI_FINISHED: 'SEMI_FINISHED',
  FINISHED: 'FINISHED',
} as const;

export const WAREHOUSE_TYPE_LABEL: Record<string, string> = {
  MATERIAL: 'Kho NVL',
  SEMI_FINISHED: 'Kho BTP',
  FINISHED: 'Kho thành phẩm',
};

export const MATERIAL_TYPE = { FABRIC: 'FABRIC', TRIM: 'TRIM' } as const;

export const MATERIAL_TYPE_LABEL: Record<string, string> = { FABRIC: 'Vải', TRIM: 'Phụ liệu' };

export const MATERIAL_CATEGORIES: Record<string, { value: string; label: string }[]> = {
  FABRIC: [
    { value: 'MAIN_FABRIC', label: 'Vải chính' },
    { value: 'LINING', label: 'Vải lót' },
    { value: 'INTERLINING', label: 'Mex / dựng' },
    { value: 'RIB', label: 'Bo / rib' },
  ],
  TRIM: [
    { value: 'THREAD', label: 'Chỉ' },
    { value: 'ZIPPER', label: 'Khóa kéo' },
    { value: 'BUTTON', label: 'Cúc' },
    { value: 'SNAP', label: 'Cúc bấm' },
    { value: 'ELASTIC', label: 'Chun' },
    { value: 'TAPE', label: 'Dây / băng' },
    { value: 'LABEL', label: 'Nhãn' },
    { value: 'HANG_TAG', label: 'Thẻ treo' },
    { value: 'PADDING', label: 'Bông / đệm' },
    { value: 'PACKAGING', label: 'Bao bì' },
    { value: 'OTHER', label: 'Khác' },
  ],
};

export const CATEGORY_LABEL: Record<string, string> = Object.fromEntries(
  Object.values(MATERIAL_CATEGORIES)
    .flat()
    .map((c) => [c.value, c.label]),
);

export const MATERIAL_STATUS = { ACTIVE: 'ACTIVE', DISCONTINUED: 'DISCONTINUED' } as const;

export const MATERIAL_STATUS_LABEL: Record<string, StatusLabel> = {
  ACTIVE: { text: 'Đang dùng', color: 'green' },
  DISCONTINUED: { text: 'Ngừng dùng', color: 'default' },
};

export const MATERIAL_SOURCE = { CUSTOMER: 'CUSTOMER', SUPPLIER: 'SUPPLIER' } as const;

export const SOURCE_LABEL: Record<string, string> = {
  CUSTOMER: 'Khách cấp',
  SUPPLIER: 'Mua NCC',
  BOTH: 'Khách cấp + mua',
};

export const RECEIPT_STATUS = {
  PENDING: 'PENDING',
  CONFIRMED: 'CONFIRMED',
  RETURNED: 'RETURNED',
} as const;

export const RECEIPT_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING: { text: 'Chờ xác nhận', color: 'gold' },
  CONFIRMED: { text: 'Đã xác nhận', color: 'green' },
  RETURNED: { text: 'Bị trả lại', color: 'red' },
};

export const RECEIPT_TYPE_LABEL: Record<string, string> = { NEW: 'Phiếu nhập', SUPPLEMENTARY: 'Phiếu bổ sung' };

/** Cách Quản đốc xử lý đơn thiếu NVL (BE: ShortageResolutions). */
export const SHORTAGE_RESOLUTION = { REPLENISH: 'REPLENISH', NO_REPLENISH: 'NO_REPLENISH' } as const;

export const SHORTAGE_RESOLUTION_LABEL: Record<string, StatusLabel> = {
  REPLENISH: { text: 'Giao bù', color: 'blue' },
  NO_REPLENISH: { text: 'Không bù', color: 'orange' },
};

/** Trạng thái đơn SHORT_DELIVERY (cùng mã với Request.Status). */
export const SHORTAGE_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING_PROCESSING: { text: 'Chờ xử lý', color: 'gold' },
  COMPLETED: { text: 'Hoàn thành', color: 'green' },
  CANCELLED: { text: 'Đã hủy', color: 'default' },
};

export const ISSUE_STATUS = {
  PENDING_RECEIPT: 'PENDING_RECEIPT',
  RECEIVED: 'RECEIVED',
  REJECTED: 'REJECTED',
} as const;

export const ISSUE_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING_RECEIPT: { text: 'Chờ tổ nhận', color: 'gold' },
  RECEIVED: { text: 'Đã nhận', color: 'green' },
  REJECTED: { text: 'Bị từ chối', color: 'red' },
};

export const STOCK_DOC_LABEL: Record<string, string> = {
  MATERIAL_RECEIPT: 'Phiếu nhập NVL',
  MATERIAL_ISSUE: 'Phiếu cấp',
  MATERIAL_RETURN: 'Phiếu trả NVL',
  CUT_HANDOVER: 'Bàn giao BTP',
  FG_HANDOVER: 'Nhận thành phẩm',
  DISPATCH_NOTE: 'Phiếu xuất hàng',
  STOCKTAKE: 'Kiểm kê',
};

export const TEAM_TYPE = { CUTTING: 'CUTTING', SEWING: 'SEWING' } as const;

/** Lệnh cắt còn cấp vải được (BE: OPEN hoặc CUTTING). */
export const ISSUABLE_WORK_ORDER_STATUSES = ['OPEN', 'CUTTING'] as const;

export const PO_GOODS_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING_FINALISATION: { text: 'Chờ chốt đặt hàng', color: 'gold' },
  ORDERED: { text: 'Đã đặt hàng', color: 'blue' },
  PARTIALLY_RECEIVED: { text: 'Nhận một phần', color: 'cyan' },
  RECEIVED: { text: 'Đã nhận đủ', color: 'green' },
  CLOSED_SHORT: { text: 'Đóng (thiếu)', color: 'orange' },
  CUSTOMER_SUPPLIED: { text: 'Khách cấp', color: 'purple' },
  REJECTED: { text: 'Bị từ chối', color: 'red' },
};

export const PO_PAYMENT_STATUS_LABEL: Record<string, StatusLabel> = {
  UNPAID: { text: 'Chưa thanh toán', color: 'gold' },
  PAID: { text: 'Đã thanh toán', color: 'green' },
  RETURNED: { text: 'Bị trả lại', color: 'red' },
};

export const LABELS = {
  purchaseOrder: 'Đơn mua',
  selectPurchaseOrder: 'Chọn đơn mua',
  purchaseLine: 'Dòng đơn mua',
  outstandingQty: 'Còn phải nhận',
  receiptSource: 'Nguồn nhập',
  receiptType: 'Loại phiếu',
  originalReceipt: 'Phiếu gốc',
  toReplenishQty: 'Cần bù',
  shortagesTitle: 'Thiếu NVL khi nhập',
  shortageCode: 'Mã yêu cầu',
  shortageResolution: 'Cách xử lý',
  shortfallQty: 'Thiếu + lỗi',
  replenishQty: 'Giao bù',
  replenishedQty: 'Đã nhận bù',
  resolveShortage: 'Xử lý thiếu',
  replenish: 'Giao bù',
  noReplenish: 'Chốt không bù',
  createSupplementary: 'Lập phiếu bổ sung',
  openShortage: 'Xem yêu cầu thiếu NVL',
  addMaterial: 'Thêm NVL mới',
  materialsTitle: 'Danh mục NVL',
  materialCode: 'Mã NVL',
  materialCodeHint: 'Để trống để hệ thống tự sinh (V-0001 / PL-0001)',
  materialName: 'Tên NVL',
  materialType: 'Loại',
  category: 'Nhóm',
  unit: 'Đơn vị tính',
  status: 'Trạng thái',
  discontinue: 'Ngừng dùng',
  reactivate: 'Dùng lại',
  orderMaterialsTitle: 'NVL của đơn hàng',
  order: 'Đơn hàng',
  selectOrder: 'Chọn đơn hàng',
  material: 'NVL',
  color: 'Màu NVL',
  materialLengthCm: 'Dài cuộn / tấm (cm)',
  materialWidthCm: 'Rộng / khổ vải (cm)',
  materialSizeHint: 'Không bắt buộc. Khổ vải dùng để ước tính số mét vải khi lập lệnh cắt bù.',
  materialColorHint: 'Để trống nếu không phân màu. Cùng loại vải khác màu là hai NVL riêng; NVL đã dùng thì không đổi màu được.',
  requiredQty: 'Số cần',
  customerQty: 'Khách cấp',
  expectedDeliveryCount: 'Số lần khách giao dự kiến',
  deliveries: 'Lần giao (đã nhận / dự kiến)',
  deliveryUnit: 'lần',
  purchaseQty: 'Phải mua',
  source: 'Nguồn',
  receivedQty: 'Đã nhập',
  note: 'Ghi chú',
  product: 'Mã hàng',
  component: 'Chi tiết',
  garmentColor: 'Màu SP',
  sizeCode: 'Size',
  wholeGarment: 'Cả bộ',
  purchaseOrdersTitle: 'Đơn mua NVL',
  poCode: 'Số đơn mua',
  purchaseOrderOrder: 'Đơn hàng',
  purchaseOrderOrderHint: 'Chọn đơn hàng để mua theo NVL của đơn; để trống nếu mua dự trữ dùng chung.',
  qtyRequiredOrCustomer: 'Phải nhập số cần hoặc số khách cấp lớn hơn 0.',
  nothingToBuy: 'Đơn hàng này chưa có NVL nào cần mua (cột "Phải mua" bằng 0). Hãy khai báo NVL của đơn trước.',
  poKindAll: 'Tất cả đơn mua',
  poKindStock: 'Mua dự trữ (không gắn đơn hàng)',
  poKindOrder: 'Gắn đơn hàng',
  poGoodsStatusFilter: 'Trạng thái hàng',
  createStockPurchase: 'Tạo đơn mua',
  stockPurchase: 'Tạo đơn mua NVL',
  selectOrderOrStock: 'Chọn đơn hàng, hoặc để trống để xem / tạo đơn mua NVL dùng chung',
  addNewMaterial: 'Thêm NVL mới',
  newMaterialHint: 'Gõ tên NVL (VD: kim may), chọn có sẵn hoặc thêm mới',
  stockPurchaseTag: 'Mua dự trữ',
  orderedQty: 'Số mua',
  unitPrice: 'Đơn giá',
  amount: 'Thành tiền',
  supplier: 'Nhà cung cấp',
  goodsStatus: 'Hàng',
  paymentStatus: 'Thanh toán',
  createdAt: 'Ngày tạo',
  receiptsTitle: 'Phiếu nhập NVL',
  receiptCode: 'Số phiếu',
  receiptDate: 'Ngày nhập',
  acceptedQty: 'Số đạt',
  submittedAt: 'Gửi lúc',
  confirmedBy: 'Người xác nhận',
  returnReason: 'Lý do trả lại',
  addLine: 'Thêm dòng',
  lineCount: 'Số dòng',
  confirm: 'Xác nhận',
  returnReceipt: 'Trả lại',
  returnTitle: 'Trả phiếu nhập cho Thủ kho',
  issuesTitle: 'Phiếu cấp NVL / BTP',
  issueCode: 'Số phiếu',
  warehouseType: 'Kho',
  workOrder: 'Lệnh cắt',
  receivingTeam: 'Tổ nhận',
  issueDate: 'Ngày cấp',
  issuedBy: 'Người cấp',
  issuedQty: 'Số cấp',
  issuedAlready: 'Đã cấp',
  issueNow: 'Cấp lần này',
  issueNeedLine: 'Nhập số cấp cho ít nhất một loại vải.',
  confirmedQty: 'Số thực nhận',
  discrepancyReason: 'Lý do chênh lệch',
  documentPhotos: 'Ảnh chứng từ',
  requestIdOptional: 'Đơn Xin NVL (nếu có)',
  requestIdRequired: 'Đơn Xin BTP',
  issueFabric: 'Cấp vải cho tổ cắt',
  issuePieces: 'Cấp BTP cho tổ may',
  remainingQty: 'Còn phải cấp',
  availableQty: 'Khả dụng',
  orderLine: 'Dòng đơn (màu - size)',
  receive: 'Xác nhận nhận',
  reject: 'Từ chối',
  rejectTitle: 'Từ chối phiếu cấp',
  inventoryTitle: 'Tồn kho',
  teamHoldings: 'BTP tổ may (đã nhận / còn trên chuyền)',
  onHandQty: 'Tồn thực',
  reservedQty: 'Đã lên phiếu',
  stockCard: 'Thẻ kho',
  transactionDate: 'Ngày',
  document: 'Chứng từ',
  qtyChange: 'Nhập (+) / Xuất (−)',
  balanceAfter: 'Tồn sau',
  pieceReceivedQty: 'Đã nhận từ kho',
  finishedQty: 'QC đạt',
  scrapQty: 'Hàng hủy',
  onLineQty: 'Còn trên chuyền',
  team: 'Tổ',
  pieceHoldingsTitle: 'BTP của tổ may',
} as const;

/** Đơn chờ khai báo NVL (Quản đốc / Admin) và màn NVL chờ nhận (Thủ kho). */
export const INCOMING_TEXT = {
  pendingTitle: 'Đơn hàng chờ khai báo / mua NVL',
  pendingHint: 'Bấm vào đơn để khai báo NVL khách cấp và NVL phải mua, rồi lập đơn mua.',
  pendingEmpty: 'Không có đơn nào đang chờ NVL.',
  open: 'Mở đơn',
  notDeclared: 'Chưa khai báo NVL',
  awaiting: 'Chờ NVL',
  partial: 'NVL về một phần',
  customerName: 'Khách hàng',
  dueDate: 'Hạn giao',
  materialLines: 'Số dòng NVL',
  customerQty: 'Khách cấp',
  toBuyQty: 'Phải mua',
  title: 'NVL chờ nhận',
  subtitle: 'NVL khách sẽ cấp và đơn mua đã đặt mà kho chưa nhận đủ. Hàng về thì bấm Lập phiếu nhập, số còn phải nhận được điền sẵn.',
  customerSection: 'Khách cấp (số lần giao dự kiến)',
  supplierSection: 'Mua nhà cung cấp (đơn mua đã đặt hàng)',
  customerEmpty: 'Chưa có NVL khách cấp nào đang chờ nhận.',
  supplierEmpty: 'Chưa có đơn mua nào đang chờ nhận.',
  deliveries: 'Lần giao (đã nhận / dự kiến)',
  deliveryUnit: 'lần',
  noDeliveryCount: 'Chưa khai',
  deliveriesDone: 'Đủ lần giao',
  material: 'NVL',
  color: 'Màu',
  order: 'Đơn hàng',
  purchaseOrder: 'Đơn mua',
  supplier: 'Nhà cung cấp',
  stockPurchase: 'Mua dự trữ',
  ordered: 'Đã đặt',
  customerAgreed: 'Khách cấp',
  received: 'Đã nhận',
  outstanding: 'Còn phải nhận',
  createReceipt: 'Lập phiếu nhập',
} as const;
