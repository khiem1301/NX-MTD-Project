import type { StatusLabel } from '@/components/common/StatusTag';

const RETURN_QTY_DECIMALS_MESSAGE = 'Tối đa 3 số lẻ.';

export const RETURN_STATUS = {
  PENDING: 'PENDING',
  CONFIRMED: 'CONFIRMED',
  REJECTED: 'REJECTED',
  CANCELLED: 'CANCELLED',
} as const;

/** Số vải trả / kho đếm tối đa 3 số lẻ, khớp cột DECIMAL(18,3). Không ép hiển thị số lẻ để 40 không thành "40.000". */
export const QTY_DECIMALS = 3;

/** Báo ngay dưới ô khi nhập quá 3 số lẻ (BE cũng chặn, 400). */
export const qtyDecimalsRule = {
  validator: async (_rule: unknown, value?: number | null) => {
    if (value === null || value === undefined) return;
    // Đếm theo chuỗi để không vướng sai số dấu phẩy động (40.123 × 1000 ≠ 40123).
    if ((String(value).split('.')[1]?.length ?? 0) > QTY_DECIMALS)
      throw new Error(RETURN_QTY_DECIMALS_MESSAGE);
  },
};

export const RETURN_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING: { text: 'Chờ kho nhận', color: 'gold' },
  CONFIRMED: { text: 'Kho đã nhận', color: 'green' },
  REJECTED: { text: 'Bị từ chối', color: 'red' },
  CANCELLED: { text: 'Đã rút', color: 'default' },
};

/** Tổ may trả NVL / BTP; tổ cắt chỉ trả NVL (theo lệnh cắt). */
export const RETURN_KIND = { MATERIAL: 'MATERIAL', SEMI_FINISHED: 'SEMI_FINISHED' } as const;

export const RETURN_KIND_LABEL: Record<string, string> = {
  MATERIAL: 'NVL / phế phẩm',
  SEMI_FINISHED: 'BTP',
};

/** Phế phẩm tính theo đơn vị này thì nhập được số lẻ; đơn vị khác (cái…) là số nguyên. Khớp MaterialTypes.ScrapDecimalUnit ở BE. */
export const SCRAP_DECIMAL_UNIT = 'kg';

export const RETURN_LABELS = {
  title: 'Trả hàng về kho',
  create: 'Lập phiếu trả',
  kind: 'Loại hàng trả',
  workOrder: 'Lệnh cắt',
  workOrderPlaceholder: 'Chọn lệnh cắt',
  orderPlaceholder: 'Chọn đơn hàng',
  item: 'Hàng',
  usedCut: 'Đã cắt',
  usedQc: 'QC đạt',
  scrapTitle: 'NVL phế phẩm trả thêm',
  scrapTag: 'phế phẩm',
  scrapAdd: 'Thêm phế phẩm',
  scrapPlaceholder: 'Chọn NVL phế phẩm',
  scrapHint: 'Phế phẩm không có trần số lượng; vải theo kg, kim theo cái.',
  materialHintSewing: 'Số còn trả được = tổng đã nhận ở mọi phiếu cấp − đã trả (không theo đơn).',
  btpHint: 'Số bộ còn trả được = đã nhận − đã QC đạt − đã trả, theo từng mã hàng – màu – size.',
  pickWorkOrder: 'Chọn lệnh cắt để xem vải còn trả được. Chỉ trả phế phẩm thì không cần chọn.',
  pickOrder: 'Chọn đơn hàng để xem BTP còn trả được.',
  nothingEntered: 'Nhập số lượng trả ít nhất một dòng.',
  code: 'Mã phiếu',
  order: 'Đơn hàng',
  team: 'Tổ',
  status: 'Trạng thái',
  createdAt: 'Ngày lập',
  totalQty: 'Tổng trả',
  confirmedBy: 'Người nhận kho',
  material: 'Hàng',
  color: 'Màu',
  unit: 'ĐVT',
  receivedQty: 'Đã nhận',
  returnedQty: 'Đã trả',
  remainingQty: 'Còn trả được',
  returnQty: 'Trả lần này',
  confirmedQty: 'Số kho đếm',
  discrepancyReason: 'Lý do lệch',
  note: 'Ghi chú',
  receive: 'Nhận vào kho',
  reject: 'Từ chối',
  rejectTitle: 'Từ chối phiếu trả',
  rejectReason: 'Lý do từ chối',
  cancel: 'Rút phiếu',
  nothingToReturn: 'Không còn hàng nào trả được theo lựa chọn này.',
  suggestHint:
    'Hệ thống đã điền sẵn số vải còn lại sau cắt của lệnh (đã nhận theo lệnh − số mét đã cắt − đã trả). Kiểm tra lại cuộn vải thực tế rồi sửa nếu khác.',
  noSuggestHint:
    'Lệnh có bàn cắt chưa nhập số mét nên hệ thống chưa tính được vải còn lại. Nhập số trả theo cuộn vải thực tế.',
} as const;

export const RETURN_MESSAGES = {
  created: 'Đã lập phiếu trả, chờ Thủ kho nhận.',
  confirmCreate: 'Lập phiếu trả hàng về kho?',
  confirmReceive: 'Xác nhận nhận hàng trả vào kho theo số đếm?',
  received: 'Đã nhận hàng trả vào kho.',
  rejected: 'Đã từ chối phiếu trả.',
  confirmCancel: 'Rút phiếu trả này?',
  cancelled: 'Đã rút phiếu trả.',
  discrepancyReasonRequired: 'Số đếm khác số trả, vui lòng nhập lý do.',
} as const;
