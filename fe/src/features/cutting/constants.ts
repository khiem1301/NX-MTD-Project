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

/** Khóa một dòng của lệnh cắt: dòng đơn + chi tiết (trống = cả bộ). Lệnh cắt bù có nhiều chi tiết cho cùng một dòng đơn. */
export const lineKey = (l: { orderLineId: number; productComponentMaterialId: number | null }) =>
  `${l.orderLineId}:${l.productComponentMaterialId ?? ''}`;

export const parseLineKey = (key: string) => {
  const [orderLineId, productComponentMaterialId] = key.split(':');
  return {
    orderLineId: Number(orderLineId),
    productComponentMaterialId: productComponentMaterialId
      ? Number(productComponentMaterialId)
      : null,
  };
};

/** Khớp WorkOrderLimits ở BE: ghi chú 600 + lý do cắt thiếu 300 luôn vừa cột ghi chú 1.000 ký tự. */
export const WORK_ORDER_LIMITS = {
  noteMax: 600,
  closeReasonMax: 300,
  /** Khớp WorkOrderLimits.MaxFabricTolerancePercent ở BE. */
  maxFabricTolerance: 100,
  fabricToleranceDecimals: 2,
} as const;

/** Khớp SaveCuttingLayDtoValidator.MaxFabricUsedM ở BE. */
export const LAY_MAX_FABRIC_USED_M = 10000;
/** Khớp SaveCuttingLayDtoValidator.MaxLineQty ở BE. */
export const LAY_MAX_LINE_QTY = 100000;
/** Số mét vải nhập tối đa 2 số lẻ (cột DECIMAL(9,2)). */
export const LAY_FABRIC_DECIMALS = 2;

export const LABELS = {
  noLines: 'Nhập số kế hoạch hoặc số chi tiết cắt lẻ cho ít nhất một dòng.',
  duplicateExtra: 'Mỗi chi tiết cắt lẻ chỉ khai một lần cho một màu - size.',
  closeShort: 'Đóng lệnh cắt thiếu',
  closeShortReason: 'Lý do cắt thiếu (chỉ đóng khi không còn phiếu chờ kho)',
  fabricReceived: 'Đã nhận',
  fabricReturned: 'Đã trả kho',
  fabricUsed: 'Thực dùng',
  createReplacement: 'Lập lệnh cắt bù',
  replacementRequest: 'Đơn cắt bù đã duyệt',
  selectReplacementRequest: 'Chọn đơn cắt bù',
  panelName: 'Chi tiết cần cắt bù',
  cancelHandover: 'Rút phiếu',
  result: 'Kết quả',
  resultSummary: 'Kế hoạch {planned} · đã cắt {cut} · đã bàn giao {handed}',
  component: 'Chi tiết',
  wholeGarment: 'Cả bộ',
  noReplacementRequest: 'Chưa có đơn cắt bù nào đã duyệt và chờ lập lệnh.',
  workOrdersTitle: 'Lệnh cắt',
  createWorkOrder: 'Lập lệnh cắt',
  selectOrder: 'Chọn đơn hàng',
  selectProduct: 'Chọn mã hàng',
  selectCuttingTeam: 'Chọn tổ cắt',
  plannedLinesHint: 'Chọn đơn hàng và mã hàng để nhập số kế hoạch cắt.',
  workOrderCode: 'Số lệnh',
  order: 'Đơn hàng',
  product: 'Mã hàng',
  cuttingTeam: 'Tổ cắt',
  dueDate: 'Hạn cắt',
  status: 'Trạng thái',
  totalPlannedQty: 'Tổng kế hoạch',
  note: 'Ghi chú',
  fabricTolerance: 'Tỉ lệ sai lệch vải',
  fabricToleranceHint: 'Trần đơn xin NVL theo lệnh = số mét hệ thống tính + tỉ lệ này.',
  orderLine: 'Màu - size',
  plannedQty: 'Kế hoạch',
  fabricNotEnough: 'Không đủ vải cho lệnh cắt',
  fabricPlan: 'Số mét vải cần và bìa nên dùng',
  fabricPlanNote: 'Ước tính.',
  fabricPlanRow: '{0}: cần khoảng {1} m. {2}',
  planGroup: 'Nhóm bìa {0}: {1}{2}',
  planExtra: 'Cắt lẻ / cắt bù, nhóm bìa {0}: {1}{2}',
  markerUse: '{0} ({1}) × {2} lớp',
  ratioPart: '{1}{0}',
  planSurplus: ' (dư {0})',
  planNotProven: ' (chưa chắc là ít mét nhất)',
  listSeparator: ', ',
  planSeparator: '; ',
  fabricNotEstimated: 'Chưa tính được vải',
  fabricEstimateNote: 'Ước tính, lệnh vẫn lưu được.',
  extraComponents: 'Cắt lẻ thêm thành phần',
  extraComponentsHint: 'Số chi tiết cắt lẻ không tính vào số đặt của đơn.',
  addExtraComponent: 'Thêm thành phần',
  extraPieces: 'Số chi tiết',
  orderedQty: 'Số đặt',
  cutQty: 'Đã cắt',
  cutBefore: 'Đã cắt trước',
  remainingToCut: 'Còn cần cắt',
  overCut: 'vượt {0}',
  handedQty: 'Đã bàn giao',
  fabrics: 'Vải của lệnh cắt',
  material: 'Vải',
  issuedQty: 'Đã cấp',
  remainingQty: 'Còn phải cấp',
  availableQty: 'Khả dụng trong kho',
  cancelWorkOrder: 'Hủy lệnh',
  laysTitle: 'Bàn cắt',
  workOrder: 'Lệnh cắt',
  selectWorkOrderHint: 'Chọn lệnh cắt.',
  workDate: 'Ngày cắt',
  shift: 'Ca',
  laySequence: 'Bàn số',
  fabricColor: 'Màu vải',
  fabricMaterial: 'Vải',
  layFabricUsed: 'Số mét vải đã dùng (m)',
  sizeSummary: 'Số lượng theo size',
  actualQty: 'Số lượng sau cắt',
  totalCutQty: 'Tổng sau cắt',
  teamFabricLeft: 'Vải còn lại của tổ (m)',
  teamFabricAfter: 'Sau bàn này',
  teamFabricUnmeasured: '{0} bàn cũ chưa có số mét',
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
