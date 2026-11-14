// Khớp DTO BE (Application/DTOs/WorkOrderDtos.cs, CuttingDtos.cs).
import type { FabricRequirement, WorkOrderListItem } from '@/types/workOrder';

export type PageParams = { page: number; pageSize: number; keyword?: string };

export type WorkOrderLine = {
  workOrderLineId: number;
  orderLineId: number;
  color: string;
  sizeCode: string;
  /** null = cả bộ; có giá trị = chi tiết lẻ của lệnh cắt bù. */
  productComponentMaterialId: number | null;
  componentName: string;
  plannedQty: number;
  cutQty: number;
  handedQty: number;
  /** Vải dòng này cần (DEV-ALL-93): cả bộ = mọi vải chi tiết của màu áo; cắt bù / cắt lẻ = vải của chi tiết. */
  fabricMaterialIds: number[] | null;
};

/** Vải theo kích thước chi tiết của một NVL: chỉ để cảnh báo, không chặn lập lệnh. materialId = 0: màu – size chưa có định mức vải. */
export type WorkOrderFabricCheck = {
  materialId: number;
  materialCode: string;
  materialName: string;
  materialColor: string;
  status: 'Enough' | 'Short' | 'Unknown';
  needMeters: number | null;
  availableQty: number | null;
  message: string | null;
  /** Bìa nên dùng theo từng nhóm bìa, hệ thống ghép cho ít mét nhất (ước tính); null = chưa tính được. */
  markerPlans: FabricMarkerPlan[] | null;
  /** Có = kho khả dụng + tổ cắt đang giữ của đơn. */
  warehouseQty: number | null;
  holdingQty: number | null;
  /** DEV-ALL-103: có bìa nên dùng rộng hơn khổ vải (vải không đủ rộng để cắt); null = không có. */
  widthWarning?: string | null;
};

/** Phương án trải bìa của một nhóm bìa trên một vải (DEV-ALL-88). isExtra = phần cắt lẻ / cắt bù; surplus = sản phẩm (cắt lẻ: miếng) dư. */
export type FabricMarkerPlan = {
  markerNo: number;
  isExtra: boolean;
  meters: number;
  surplus: number;
  isProvenOptimal: boolean;
  markers: FabricMarkerUse[];
};

/** Một bìa trong phương án: plies lớp × lengthM = meters; ratio theo thứ tự size. */
export type FabricMarkerUse = {
  markerCode: string;
  lengthM: number;
  plies: number;
  meters: number;
  ratio: { sizeCode: string; piecesPerPly: number }[];
  /** Số bìa trải song song trên một lớp (vải rộng gấp k lần bìa, DEV-ALL-105); thiếu = 1. */
  across?: number;
};

export type WorkOrder = Omit<WorkOrderListItem, 'totalPlannedQty'> & {
  productName: string;
  sourceRequestId: number | null;
  note: string | null;
  lines: WorkOrderLine[];
  fabrics: FabricRequirement[];
  fabricChecks: WorkOrderFabricCheck[];
  /** Tỉ lệ sai lệch (%) trên số mét hệ thống tính: trần đơn xin NVL theo lệnh. */
  fabricTolerancePercent: number;
};

export type WorkOrderQuery = PageParams & { status?: string; orderId?: number };

export type SaveWorkOrderPayload = {
  orderId: number;
  productId: number;
  cuttingTeamId: number;
  dueDate: string;
  note?: string;
  fabricTolerancePercent?: number;
  lines: SaveWorkOrderLine[];
};

export type SaveWorkOrderLine = {
  orderLineId: number;
  plannedQty: number;
  /** Có giá trị = cắt lẻ thêm chi tiết này, plannedQty là số chi tiết. */
  productComponentMaterialId?: number | null;
};

export type CheckWorkOrderFabricPayload = {
  orderId: number;
  cuttingTeamId: number;
  lines: SaveWorkOrderLine[];
};

export type CuttingLayLine = {
  cuttingLayLineId: number;
  orderLineId: number;
  /** null = cả bộ; có giá trị = chi tiết lẻ của lệnh cắt bù. */
  productComponentMaterialId: number | null;
  componentName: string | null;
  color: string;
  sizeCode: string;
  /** Số lượng sau cắt (cắt bù: số chi tiết). */
  actualQty: number;
};

export type CuttingLayFabric = {
  materialId: number;
  materialCode: string;
  materialName: string;
  materialColor: string;
  /** null = bàn cũ chưa có số mét. */
  usedM: number | null;
};

export type CuttingLay = {
  cuttingLayId: number;
  workOrderId: number;
  workOrderCode: string;
  workDate: string;
  shiftCode: string;
  laySequence: number;
  /** Số mét từng vải của bàn (DEV-ALL-93). */
  fabrics: CuttingLayFabric[];
  /** Số lượng sau cắt theo size, VD "M:90, L:60". */
  sizeSummary: string;
  note: string | null;
  status: string;
  createdAt: string;
  lines: CuttingLayLine[];
};

export type CuttingLayQuery = PageParams & {
  workOrderId?: number;
  workDate?: string;
  status?: string;
};

export type SaveCuttingLayPayload = {
  workOrderId: number;
  workDate: string;
  shiftCode: string;
  /** Đủ mọi vải các dòng cắt cần (DEV-ALL-93). */
  fabrics: { materialId: number; usedM: number }[];
  note?: string;
  lines: {
    orderLineId: number;
    productComponentMaterialId: number | null;
    actualQty: number;
  }[];
};

export type CutHandoverListItem = {
  cutHandoverId: number;
  handoverCode: string;
  workOrderId: number;
  workOrderCode: string;
  orderCode: string;
  cuttingTeamId: number;
  cuttingTeamName: string;
  handedAt: string;
  status: string;
  confirmedByName: string | null;
  confirmedAt: string | null;
  totalHandedQty: number;
};

export type CutHandoverLine = {
  cutHandoverLineId: number;
  orderLineId: number;
  productComponentMaterialId: number | null;
  componentName: string | null;
  productCode: string;
  color: string;
  sizeCode: string;
  handedQty: number;
  confirmedQty: number | null;
  discrepancyReason: string | null;
};

export type CutHandover = Omit<CutHandoverListItem, 'totalHandedQty'> & {
  rejectReason: string | null;
  lines: CutHandoverLine[];
};

export type HandoverableLine = {
  orderLineId: number;
  productComponentMaterialId: number | null;
  componentName: string | null;
  color: string;
  sizeCode: string;
  cutQty: number;
  handedQty: number;
  remainingQty: number;
};

export type CutHandoverQuery = PageParams & { status?: string; workOrderId?: number };

export type CreateHandoverPayload = {
  workOrderId: number;
  lines: { orderLineId: number; productComponentMaterialId: number | null; handedQty: number }[];
};

export type ConfirmHandoverPayload = {
  lines: { cutHandoverLineId: number; confirmedQty: number; discrepancyReason?: string }[];
};

/** Đơn cắt bù đã được Quản đốc duyệt và chưa có lệnh cắt bù (UC19, TH-10). */
export type ReplacementRequestLine = {
  orderLineId: number;
  color: string;
  sizeCode: string;
  productComponentMaterialId: number;
  componentName: string;
  quantity: number;
};

export type ReplacementRequest = {
  requestId: number;
  requestCode: string;
  orderId: number;
  orderCode: string;
  productCode: string;
  content: string | null;
  lines: ReplacementRequestLine[];
};

export type CreateReplacementPayload = {
  requestId: number;
  cuttingTeamId: number;
  dueDate: string;
  note?: string;
};
