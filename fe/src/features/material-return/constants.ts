import type { StatusLabel } from '@/components/common/StatusTag';

export const RETURN_STATUS = {
  PENDING: 'PENDING',
  CONFIRMED: 'CONFIRMED',
  REJECTED: 'REJECTED',
  CANCELLED: 'CANCELLED',
} as const;

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
