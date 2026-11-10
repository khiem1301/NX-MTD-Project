import type { StatusLabel } from '@/components/common/StatusTag';

/** Loại kho dùng chung với kiểm kê: khai báo ở constants/warehouse. */
export { WAREHOUSE_TYPE, WAREHOUSE_TYPE_LABEL } from '@/constants/warehouse';

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

export const RECEIPT_TYPE_LABEL: Record<string, string> = {
  NEW: 'Phiếu nhập',
  SUPPLEMENTARY: 'Phiếu bổ sung',
};

/** Cách Quản đốc xử lý đơn thiếu NVL (BE: ShortageResolutions). */
export const SHORTAGE_RESOLUTION = {
  REPLENISH: 'REPLENISH',
  NO_REPLENISH: 'NO_REPLENISH',
} as const;

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

/** Số ảnh chứng từ tối đa của một phiếu nhập. */
export const MAX_RECEIPT_PHOTOS = 5;

export const MATERIAL_ORDER_SCOPE = { NO_ORDER: 'NO_ORDER' } as const;

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
  toReplenishQtyHint:
    'Số còn thiếu sau phiếu này: số đặt (hoặc khách cấp) trừ số đã nhận ở các lần trước và lần này. Phiếu bổ sung: số Quản đốc chọn giao bù.',
  deliveryNo: 'Lần nhận',
  deliveryNoHint: 'Phiếu này là lần nhận thứ mấy của NVL / số lần giao dự kiến',
  deliveryNoValue: '{0}/{1}',
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
  bulkEditOrderMaterials: 'Sửa cả đơn',
  addByProduct: 'Thêm theo mã hàng',
  addExternalMaterial: 'Thêm NVL ngoài',
  selectProduct: 'Chọn mã hàng trong đơn',
  productMaterialsHint:
    'Nhập số cho từng NVL của mã hàng. NVL đơn này không dùng thì xóa khỏi bảng trước khi lưu.',
  productAllDeclared: 'Các NVL của mã hàng này đã có trong đơn.',
  productNoNorm: 'Mã hàng này chưa có NVL trong định mức. Dùng Thêm NVL ngoài để thêm vào đơn.',
  order: 'Đơn hàng',
  material: 'NVL',
  color: 'Màu NVL',
  materialColorHint:
    'Để trống nếu không phân màu. Cùng loại vải khác màu là hai NVL riêng; NVL đã dùng thì không đổi màu được.',
  requiredQty: 'Số cần',
  customerQty: 'Khách cấp',
  expectedDeliveryCount: 'Số lần khách giao dự kiến',
  expectedDeliveryDate: 'Ngày dự kiến giao',
  expectedDeliveryDateRequired: 'Vui lòng chọn ngày dự kiến giao.',
  deliveryCountShort: 'Số lần giao',
  deliveries: 'Lần giao',
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
  purchaseOrderOrderHint:
    'Chọn đơn hàng để mua theo NVL của đơn (không cần mua hết, xóa dòng chưa mua); để trống nếu mua dự trữ dùng chung.',
  addOrderMaterial: 'Thêm NVL của đơn chưa chọn',
  noActiveSupplier:
    'Chưa có nhà cung cấp nào đang hợp tác. Nhờ Admin thêm nhà cung cấp trước khi tạo đơn mua.',
  qtyRequiredOrCustomer: 'Phải nhập số cần hoặc số khách cấp lớn hơn 0.',
  requiredQtyName: 'Số cần',
  customerQtyName: 'Số khách cấp',
  qtyNegative: '{0} không được âm.',
  qtyMax: '{0} tối đa {1}.',
  qtyScale: '{0} tối đa {1} số lẻ.',
  deliveryCountRange: 'Số lần giao dự kiến phải là số nguyên từ 1 đến {0}.',
  noteMax: 'Ghi chú tối đa {0} ký tự.',
  nothingToBuy:
    'Đơn hàng này chưa có NVL nào cần mua (cột "Phải mua" bằng 0). Hãy khai báo NVL của đơn trước.',
  poKindAll: 'Tất cả đơn mua',
  poKindStock: 'Mua dự trữ (không gắn đơn hàng)',
  poKindOrder: 'Gắn đơn hàng',
  poGoodsStatusFilter: 'Trạng thái hàng',
  createStockPurchase: 'Tạo đơn mua',
  stockPurchase: 'Tạo đơn mua NVL',
  addNewMaterial: 'Thêm NVL mới',
  newMaterialHint: 'Gõ tên NVL (VD: kim may), chọn có sẵn hoặc thêm mới',
  stockPurchaseTag: 'Mua dự trữ',
  orderedQty: 'Số mua',
  unitPrice: 'Đơn giá',
  amount: 'Thành tiền',
  purchaseOrderTotal: 'Tổng tiền',
  supplier: 'Nhà cung cấp',
  orderDate: 'Ngày đặt hàng',
  orderDateRequired: 'Vui lòng chọn ngày đặt hàng.',
  poExpectedDeliveryCount: 'Số lần nhà cung cấp dự kiến giao',
  expectedDeliveryRequired: 'Nhập số lần giao dự kiến (từ 1 đến 99).',
  priceRequired: 'Nhập đơn giá lớn hơn 0.',
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
  addReceiptLine: 'Thêm NVL còn phải nhận chưa có trong phiếu',
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
  suggestTitle: 'Đề xuất theo mã hàng · màu · size',
  suggestProduct: 'Mã hàng',
  suggestColor: 'Màu',
  suggestSize: 'Size (trống = mọi size)',
  suggestFabricAction: 'Đề xuất số cấp',
  suggestPieceAction: 'Thêm dòng đề xuất',
  suggestNothing:
    'Không có gì để đề xuất: kho hết hàng, đã có trong phiếu hoặc chưa tính được nhu cầu.',
  suggestApplied: 'Đã đề xuất {0} dòng.',
  confirmedQty: 'Số thực nhận',
  discrepancyReason: 'Lý do chênh lệch',
  documentPhotos: 'Ảnh chứng từ',
  documentPhotosHint:
    'Đính kèm ảnh chứng từ giao hàng trước khi lưu phiếu; sau khi lưu không thêm ảnh ở màn chi tiết nữa.',
  photoUploadFailed: 'Đã lưu phiếu nhưng {0} ảnh chưa tải được. Mở phiếu sửa để thêm lại ảnh.',
  requestIdOptional: 'Đơn Xin NVL (nếu có)',
  requestIdRequired: 'Đơn Xin BTP',
  issueFabric: 'Cấp vải cho tổ cắt',
  issuePieces: 'Cấp BTP cho tổ may',
  remainingQty: 'Còn phải cấp',
  availableQty: 'Khả dụng',
  orderFilter: 'Đơn hàng (đơn đặt của khách)',
  orderScopeNoOrder: 'Dùng chung',
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

/** Bộ lọc bảng NVL của đơn. */
export const ORDER_MATERIAL_FILTER_TEXT = {
  search: 'Tìm mã, tên, màu NVL',
  supply: 'Nguồn',
  supplyCustomer: 'Khách cấp',
  supplyPurchase: 'Phải mua',
  receipt: 'Nhập kho',
  receiptPending: 'Chưa nhận đủ',
  receiptDone: 'Đã nhận đủ',
} as const;

/** Màn NVL chờ nhận (Thủ kho). */
export const INCOMING_TEXT = {
  title: 'NVL chờ nhận',
  subtitle:
    'NVL khách sẽ cấp và đơn mua đã đặt mà kho chưa nhận đủ. Hàng về thì bấm Lập phiếu nhập, số còn phải nhận được điền sẵn.',
  customerSection: 'Khách cấp (số lần giao dự kiến)',
  supplierSection: 'Mua nhà cung cấp (đơn mua đã đặt hàng)',
  customerEmpty: 'Chưa có NVL khách cấp nào đang chờ nhận.',
  supplierEmpty: 'Chưa có đơn mua nào đang chờ nhận.',
  goodsStatus: 'Tình trạng đơn mua',
  canReceiveMore: 'Còn nhận tiếp được',
  closedShortSection: 'Đơn mua đã đóng do giao thiếu (không nhận tiếp)',
  closedShortHint:
    'Phần còn thiếu đã được chốt không bù, không lập thêm phiếu nhập cho các đơn này.',
  closedShortEmpty: 'Chưa có đơn mua nào đóng do giao thiếu.',
  notReplenished: 'Thiếu đã chốt không bù',
  deliveries: 'Lần giao',
  expectedDeliveryDate: 'Ngày dự kiến giao',
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
