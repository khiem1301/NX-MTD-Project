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

/** Một dòng giao việc của ngày: công nhân × dòng đơn × công đoạn. `allocatedQty` là trần sản lượng cuối ca của ngày. */
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
  allocatedQty: number;
  outputQty: number;
  remainingQty: number;
  unallocatedQty: number;
};

export type DailyAllocationBoard = { workDate: string; rows: DailyAllocationRow[] };

export type SaveDailyItem = {
  workerId: number;
  orderLineId: number;
  operationId: number;
  quantity: number;
};
export type SaveDailyPayload = { workDate: string; items: SaveDailyItem[]; reason?: string };
export type RetrieveDailyPayload = { workDate: string; workerIds?: number[]; reason: string };
export type DailyAllocationResult = { added: number; adjusted: number; retrieved: number };
