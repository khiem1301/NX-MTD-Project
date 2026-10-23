import type { StatusLabel } from '@/components/common/StatusTag';

export const STOCKTAKE_STATUS = {
  PENDING_APPROVAL: 'PENDING_APPROVAL',
  APPROVED: 'APPROVED',
  RETURNED: 'RETURNED',
} as const;

export const STOCKTAKE_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING_APPROVAL: { text: 'Chờ duyệt', color: 'gold' },
  APPROVED: { text: 'Đã duyệt', color: 'green' },
  RETURNED: { text: 'Bị trả lại', color: 'red' },
};

export const ST_LABELS = {
  title: 'Kiểm kê kho',
  create: 'Lập phiếu kiểm kê',
  edit: 'Sửa và gửi lại',
  code: 'Mã phiếu',
  warehouse: 'Kho',
  scope: 'Phạm vi',
  scopeHint: 'VD: kệ A, vải đơn DH-01',
  date: 'Ngày kiểm kê',
  status: 'Trạng thái',
  createdAt: 'Ngày lập',
  lines: 'Số dòng',
  diffLines: 'Dòng lệch',
  item: 'Hàng hóa',
  unit: 'ĐVT',
  bookQty: 'Số sổ',
  countedQty: 'Số đếm',
  differenceQty: 'Chênh lệch',
  note: 'Ghi chú',
  approve: 'Duyệt và điều chỉnh kho',
  return: 'Trả lại',
  returnTitle: 'Trả lại phiếu kiểm kê',
  returnReason: 'Lý do trả lại',
  orderFilter: 'Lọc theo đơn hàng',
  blankHint: 'Để trống số đếm nếu khớp sổ.',
  noRows: 'Kho này chưa có hàng để kiểm kê.',
} as const;

export const ST_MESSAGES = {
  saved: 'Đã gửi phiếu kiểm kê cho Quản đốc / Admin duyệt.',
  confirmApprove: 'Duyệt phiếu? Tồn kho sẽ được điều chỉnh theo số đếm ở các dòng lệch.',
  approved: 'Đã duyệt phiếu kiểm kê.',
  returned: 'Đã trả phiếu kiểm kê cho Thủ kho.',
} as const;
