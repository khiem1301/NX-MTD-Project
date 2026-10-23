import type { StatusLabel } from '@/components/common/StatusTag';

export const FG_STATUS = {
  PENDING: 'PENDING',
  CONFIRMED: 'CONFIRMED',
  REJECTED: 'REJECTED',
  CANCELLED: 'CANCELLED',
} as const;

export const FG_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING: { text: 'Chờ kho nhận', color: 'gold' },
  CONFIRMED: { text: 'Kho đã nhận', color: 'green' },
  REJECTED: { text: 'Bị từ chối', color: 'red' },
  CANCELLED: { text: 'Đã rút', color: 'default' },
};

export const FG_LABELS = {
  title: 'Bàn giao thành phẩm',
  create: 'Lập phiếu bàn giao',
  code: 'Mã phiếu',
  order: 'Đơn hàng',
  product: 'Mã hàng',
  inspector: 'QA/QC giao',
  handedAt: 'Giờ giao',
  status: 'Trạng thái',
  confirmedBy: 'Người nhận kho',
  totalHandedQty: 'Số giao',
  orderLine: 'Màu - size',
  qcQty: 'QC đạt',
  handedQty: 'Đã giao',
  remainingQty: 'Còn giao được',
  handoverQty: 'Giao lần này',
  confirmedQty: 'Số kho đếm',
  discrepancyReason: 'Lý do lệch',
  receive: 'Nhận vào kho',
  reject: 'Từ chối',
  rejectTitle: 'Từ chối phiếu bàn giao thành phẩm',
  rejectReason: 'Lý do từ chối',
  cancel: 'Rút phiếu',
  selectOrder: 'Chọn đơn hàng',
  selectProduct: 'Chọn mã hàng',
  nothingToHand: 'Đơn này chưa có số QC đạt nào còn giao được.',
} as const;

export const FG_MESSAGES = {
  created: 'Đã lập phiếu bàn giao thành phẩm, chờ Thủ kho nhận.',
  confirmCreate:
    'Lập phiếu bàn giao thành phẩm? Số giao sẽ được giữ cho phiếu này đến khi kho nhận hoặc từ chối.',
  confirmReceive: 'Xác nhận nhận thành phẩm vào kho theo số đếm?',
  received: 'Đã nhận thành phẩm vào kho.',
  rejected: 'Đã từ chối phiếu bàn giao thành phẩm.',
  confirmCancel: 'Rút phiếu bàn giao này?',
  cancelled: 'Đã rút phiếu bàn giao thành phẩm.',
  discrepancyReasonRequired: 'Số đếm khác số giao, vui lòng nhập lý do.',
} as const;
