import type { StatusLabel } from '@/components/common/StatusTag';

// Giá trị khớp CHECK constraint DB (WorkOrder, CuttingLay, CutHandover).
export const WORK_ORDER_STATUS = {
  OPEN: 'OPEN',
  CUTTING: 'CUTTING',
  COMPLETED: 'COMPLETED',
  CANCELLED: 'CANCELLED',
} as const;

export const WORK_ORDER_STATUS_LABEL: Record<string, StatusLabel> = {
  OPEN: { text: 'Mới lập', color: 'blue' },
  CUTTING: { text: 'Đang cắt', color: 'gold' },
  COMPLETED: { text: 'Hoàn thành', color: 'green' },
  CANCELLED: { text: 'Đã hủy', color: 'default' },
};

export const LAY_STATUS = { RECORDED: 'RECORDED', LOCKED: 'LOCKED' } as const;

export const LAY_STATUS_LABEL: Record<string, StatusLabel> = {
  RECORDED: { text: 'Đã ghi', color: 'blue' },
  LOCKED: { text: 'Đã bàn giao (khóa)', color: 'default' },
};

export const HANDOVER_STATUS = {
  PENDING: 'PENDING',
  CONFIRMED: 'CONFIRMED',
  REJECTED: 'REJECTED',
  CANCELLED: 'CANCELLED',
} as const;

export const HANDOVER_STATUS_LABEL: Record<string, StatusLabel> = {
  PENDING: { text: 'Chờ kho nhận', color: 'gold' },
  CONFIRMED: { text: 'Kho đã nhận', color: 'green' },
  REJECTED: { text: 'Bị từ chối', color: 'red' },
  CANCELLED: { text: 'Đã hủy', color: 'default' },
};

export const SHIFT_OPTIONS = [
  { value: 'MORNING', label: 'Ca sáng' },
  { value: 'AFTERNOON', label: 'Ca chiều' },
  { value: 'OVERTIME', label: 'Tăng ca' },
];

export const SHIFT_LABEL: Record<string, string> = Object.fromEntries(
  SHIFT_OPTIONS.map((s) => [s.value, s.label]),
);

export const CUTTING_TEAM_TYPE = 'CUTTING';

export const LABELS = {
  workOrdersTitle: 'Lệnh cắt',
  workOrderCode: 'Số lệnh',
  order: 'Đơn hàng',
  product: 'Mã hàng',
  cuttingTeam: 'Tổ cắt',
  dueDate: 'Hạn cắt',
  status: 'Trạng thái',
  totalPlannedQty: 'Tổng kế hoạch',
  note: 'Ghi chú',
  orderLine: 'Màu - size',
  plannedQty: 'Kế hoạch',
  orderedQty: 'Số đặt',
  cutQty: 'Đã cắt',
  handedQty: 'Đã bàn giao',
  fabrics: 'Vải cần theo định mức',
  material: 'Vải',
  requiredQty: 'Cần',
  issuedQty: 'Đã cấp',
  remainingQty: 'Còn phải cấp',
  availableQty: 'Khả dụng trong kho',
  cancelWorkOrder: 'Hủy lệnh',
  laysTitle: 'Bàn cắt',
  workOrder: 'Lệnh cắt',
  selectWorkOrderHint: 'Chọn lệnh cắt của tổ để xem và nhập bàn cắt.',
  workDate: 'Ngày cắt',
  shift: 'Ca',
  laySequence: 'Bàn số',
  fabricColor: 'Màu vải',
  fabricMaterial: 'Vải (tùy chọn)',
  plyCount: 'Số lớp',
  sizeRatio: 'Tỉ lệ size',
  ratioValue: 'Tỉ lệ',
  calculatedQty: 'Số tính',
  actualQty: 'Số thực tế',
  adjustReason: 'Lý do sửa số',
  handoversTitle: 'Bàn giao BTP vào kho',
  handoverCode: 'Số phiếu',
  handedAt: 'Giao lúc',
  totalHandedQty: 'Tổng giao',
  confirmedBy: 'Kho xác nhận',
  confirmedQty: 'Số kho đếm',
  discrepancyReason: 'Lý do chênh lệch',
  handoverQty: 'Số giao',
  createHandover: 'Lập phiếu bàn giao',
  receive: 'Nhận vào kho',
  reject: 'Từ chối',
  rejectTitle: 'Từ chối phiếu bàn giao',
  rejectReason: 'Lý do từ chối',
} as const;
