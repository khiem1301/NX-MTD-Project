import type { PageParams } from '@/types/api';

/** Một lần giao / thu hồi việc với công nhân: số lần làm công đoạn × màu × size (26/10: không còn BTP giao kèm). */
export type WorkAllocation = {
  workAllocationId: number;
  workDate: string;
  movementType: string;
  sewingTeamId: number;
  teamName: string;
  workerId: number;
  workerCode: string;
  workerName: string;
  orderLineId: number;
  orderCode: string;
  productCode: string;
  color: string;
  sizeCode: string;
  operationId: number;
  operationCode: string;
  operationName: string;
  quantity: number;
  confirmedQty: number | null;
  discrepancyReason: string | null;
  retrievalReason: string | null;
  retrievalNote: string | null;
  confirmMode: string;
  onBehalfReason: string | null;
  status: string;
  rejectReason: string | null;
  createdAt: string;
  confirmedAt: string | null;
  /** Đơn giá công đoạn hiệu lực theo ngày giao; null = chưa có đơn giá duyệt. */
  unitRate: number | null;
  rateApproved: boolean;
};

/** Tiến độ chia việc của một công đoạn × dòng đơn. */
export type OperationProgress = {
  orderLineId: number;
  orderCode: string;
  productCode: string;
  color: string;
  sizeCode: string;
  operationId: number;
  operationCode: string;
  operationName: string;
  groupName: string | null;
  requiredQty: number;
  allocatedQty: number;
  pendingQty: number;
  unallocatedQty: number;
  outputQty: number;
};

/** Số lần một công nhân đang được giao ở một công đoạn × dòng đơn. */
export type WorkerAllocationLine = {
  workerId: number;
  workerCode: string;
  workerName: string;
  orderLineId: number;
  operationId: number;
  heldQty: number;
  pendingRetrievalQty: number;
  outputQty: number;
  retrievableQty: number;
};

export type WorkAllocationBoard = {
  operations: OperationProgress[];
  workers: WorkerAllocationLine[];
};

export type AllocationQuery = PageParams & {
  status?: string;
  movementType?: string;
  workerId?: number;
  orderId?: number;
};

export type ConfirmAllocationPayload = {
  confirmedQty: number;
  discrepancyReason?: string;
};
export type ConfirmOnBehalfPayload = ConfirmAllocationPayload & { onBehalfReason: string };

export type AllocationFilters = { status?: string; movementType?: string };

/**
 * Một dòng giao việc của ngày: công nhân × dòng đơn × công đoạn. Trần sản lượng cuối ca của ngày = `carriedQty` (tồn từ hôm trước)
 * + `allocatedQty` (giao thêm trong ngày); phần chưa làm hết chuyển sang hôm sau, không thu hồi.
 */
export type DailyAllocationRow = {
  workerId: number;
  workerCode: string;
  workerName: string;
  orderLineId: number;
  orderCode: string;
  productCode: string;
  color: string;
  sizeCode: string;
  operationId: number;
  operationCode: string;
  operationName: string;
  groupName: string | null;
  /** Công đoạn đã phân công cho công nhân trước khi may; false = tổ trưởng thêm ngoài phân công. */
  isAssigned: boolean;
  carriedQty: number;
  allocatedQty: number;
  outputQty: number;
  remainingQty: number;
  unallocatedQty: number;
  /** Số giao của lần giao gần nhất trước ngày này; 0 = chưa giao lần nào. */
  previousQty: number;
  unitRate: number | null;
  rateApproved: boolean;
};

export type DailyAllocationBoard = { workDate: string; rows: DailyAllocationRow[] };

export type SaveDailyItem = {
  workerId: number;
  orderLineId: number;
  operationId: number;
  quantity: number;
};
export type SaveDailyPayload = { workDate: string; items: SaveDailyItem[]; reason?: string };
export type DailyAllocationResult = { added: number; adjusted: number };
