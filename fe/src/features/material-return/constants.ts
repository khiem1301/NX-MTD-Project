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

export const RETURN_LABELS = {
  title: 'Trả vải thừa',
  create: 'Lập phiếu trả vải',
  code: 'Mã phiếu',
  issue: 'Phiếu cấp gốc',
  order: 'Đơn hàng',
  team: 'Tổ cắt',
  status: 'Trạng thái',
  createdAt: 'Ngày lập',
  totalQty: 'Tổng trả',
  confirmedBy: 'Người nhận kho',
  material: 'NVL',
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
  rejectTitle: 'Từ chối phiếu trả vải',
  rejectReason: 'Lý do từ chối',
  cancel: 'Rút phiếu',
  nothingToReturn: 'Tổ chưa nhận vải nào còn trả được.',
  suggestedQty: 'Còn lại sau cắt',
  suggestHint:
    'Hệ thống đã điền sẵn số vải còn lại sau các bàn cắt (đã nhận − đã trả − số lớp × chiều dài bàn cắt). Kiểm tra lại cuộn vải thực tế rồi sửa nếu khác.',
  noSuggestHint:
    'Có bàn cắt chưa nhập chiều dài nên hệ thống chưa tính được vải còn lại. Nhập số trả theo cuộn vải thực tế.',
} as const;

export const RETURN_MESSAGES = {
  created: 'Đã lập phiếu trả vải, chờ Thủ kho nhận.',
  confirmCreate: 'Lập phiếu trả vải thừa?',
  confirmReceive: 'Xác nhận nhận vải trả vào kho theo số đếm?',
  received: 'Đã nhận vải trả vào kho.',
  rejected: 'Đã từ chối phiếu trả vải.',
  confirmCancel: 'Rút phiếu trả vải này?',
  cancelled: 'Đã rút phiếu trả vải.',
  discrepancyReasonRequired: 'Số đếm khác số trả, vui lòng nhập lý do.',
} as const;
