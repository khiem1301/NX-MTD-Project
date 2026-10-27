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
};

export type WorkOrder = Omit<WorkOrderListItem, 'totalPlannedQty'> & {
  productName: string;
  sourceRequestId: number | null;
  note: string | null;
  lines: WorkOrderLine[];
  fabrics: FabricRequirement[];
};

export type WorkOrderQuery = PageParams & { status?: string; orderId?: number };

export type SaveWorkOrderPayload = {
  orderId: number;
  productId: number;
  cuttingTeamId: number;
  dueDate: string;
  note?: string;
  lines: { orderLineId: number; plannedQty: number }[];
};

export type CuttingLayLine = {
  cuttingLayLineId: number;
  orderLineId: number;
  /** null = cả bộ; có giá trị = chi tiết lẻ của lệnh cắt bù. */
  productComponentMaterialId: number | null;
  componentName: string | null;
  color: string;
  sizeCode: string;
  ratioValue: number;
  calculatedQty: number;
  actualQty: number;
  adjustReason: string | null;
};

export type CuttingLay = {
  cuttingLayId: number;
  workOrderId: number;
  workOrderCode: string;
  workDate: string;
  shiftCode: string;
  laySequence: number;
  fabricMaterialId: number | null;
  fabricMaterialCode: string | null;
  fabricColor: string;
  plyCount: number;
  sizeRatio: string;
  note: string | null;
  status: string;
  createdAt: string;
  lines: CuttingLayLine[];
};

export type CuttingLayQuery = PageParams & { workOrderId?: number; workDate?: string };

export type SaveCuttingLayPayload = {
  workOrderId: number;
  workDate: string;
  shiftCode: string;
  fabricMaterialId?: number;
  fabricColor: string;
  plyCount: number;
  note?: string;
  lines: {
    orderLineId: number;
    productComponentMaterialId: number | null;
    ratioValue: number;
    actualQty?: number;
    adjustReason?: string;
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
