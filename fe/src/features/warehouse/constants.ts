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

export const LABELS = {
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
  requiredQty: 'Số cần',
  normRequiredQty: 'Cần theo định mức',
  customerQty: 'Khách cấp',
  customerExpectedDate: 'Ngày khách giao dự kiến',
  purchaseQty: 'Phải mua',
  source: 'Nguồn',
  receivedQty: 'Đã nhập',
  note: 'Ghi chú',
  norms: 'Định mức',
  addNorm: 'Thêm dòng định mức',
  product: 'Mã hàng',
  component: 'Chi tiết',
  garmentColor: 'Màu SP (trống = mọi màu)',
  sizeCode: 'Size (trống = mọi size)',
  qtyPerPiece: 'Định mức / SP',
  wastagePercent: '% hao hụt',
  receiptsTitle: 'Phiếu nhập NVL',
  receiptCode: 'Số phiếu',
  receiptDate: 'Ngày nhập',
  acceptedQty: 'Số đạt',
  shortQty: 'Số thiếu',
  defectiveQty: 'Số lỗi',
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
  confirmedQty: 'Số thực nhận',
  discrepancyReason: 'Lý do chênh lệch',
  requestId: 'Mã đơn xin cấp (nếu có)',
  issueFabric: 'Cấp vải cho tổ cắt',
  issuePieces: 'Cấp BTP cho tổ may',
  remainingQty: 'Còn phải cấp',
  availableQty: 'Khả dụng',
  orderLine: 'Dòng đơn (màu - size)',
  receive: 'Xác nhận nhận',
  reject: 'Từ chối',
  rejectTitle: 'Từ chối phiếu cấp',
  inventoryTitle: 'Tồn kho',
  teamHoldings: 'BTP tổ may đang giữ',
  onHandQty: 'Tồn thực',
  reservedQty: 'Đang giữ chỗ',
  stockCard: 'Thẻ kho',
  transactionDate: 'Ngày',
  document: 'Chứng từ',
  qtyChange: 'Nhập (+) / Xuất (−)',
  balanceAfter: 'Tồn sau',
  receivedFromWarehouse: 'Đã nhận từ kho',
  handedToWorkers: 'Đã giao CN',
  retrievedFromWorkers: 'Đã thu hồi',
  heldByTeam: 'Tổ đang giữ',
  team: 'Tổ',
  pieceHoldingsTitle: 'BTP tổ đang giữ',
} as const;
