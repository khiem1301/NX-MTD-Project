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
export const WORK_ORDER_LIMITS = { noteMax: 600, closeReasonMax: 300 } as const;

/** Khớp SaveCuttingLayDtoValidator.MaxLayLengthM ở BE. */
export const LAY_MAX_LENGTH_M = 1000;
/** Số lớp và tỉ lệ size tối đa, khớp cột SMALLINT của bàn cắt. */
export const LAY_MAX_SMALLINT = 32767;

export const LABELS = {
  noLines: 'Nhập số kế hoạch hoặc số chi tiết cắt lẻ cho ít nhất một dòng.',
  duplicateExtra: 'Mỗi chi tiết cắt lẻ chỉ khai một lần cho một màu - size.',
  closeShort: 'Đóng lệnh cắt thiếu',
  closeShortReason:
    'Lý do cắt thiếu kế hoạch. Cắt đủ thì lệnh tự hoàn thành khi kho nhận đủ BTP, không cần đóng. Chỉ đóng được khi không còn phiếu bàn giao, phiếu trả vải chờ kho và BTP đã cắt đã vào kho hết.',
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
  orderLine: 'Màu - size',
  plannedQty: 'Kế hoạch',
  fabricNotEnough: 'Không đủ vải cho lệnh cắt',
  fabricRatioWarning: 'Bìa lệch tỉ lệ lệnh cắt',
  fabricNotEstimated: 'Chưa tính được vải',
  fabricEstimateNote:
    'Số mét là ước tính theo kích thước bìa và khổ vải, có thể dư so với sơ đồ giác thực tế. Lệnh vẫn lưu được.',
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
  selectWorkOrderHint: 'Chọn lệnh cắt của tổ để xem và nhập bàn cắt.',
  workDate: 'Ngày cắt',
  shift: 'Ca',
  laySequence: 'Bàn số',
  fabricColor: 'Màu vải',
  fabricMaterial: 'Vải',
  plyCount: 'Số lớp',
  layLength: 'Chiều dài bàn cắt (m)',
  layFabricCut: 'Vải đã cắt (m)',
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
