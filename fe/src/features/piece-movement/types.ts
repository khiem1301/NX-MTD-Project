import type { PageParams } from '@/types/api';

/** Một lần giao / thu hồi việc với công nhân: số lần làm công đoạn × màu × size (26/10: không còn BTP giao kèm). */
export type WorkAllocation = {
  workAllocationId: number;
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
  usesSemiFinished: boolean;
  quantity: number;
  confirmedQty: number | null;
  discrepancyReason: string | null;
  retrievalReason: string | null;
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
  usesSemiFinished: boolean;
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

export type CreateAllocationPayload = {
  movementType: string;
  workerId: number;
  orderLineId: number;
  operationId: number;
  quantity: number;
  retrievalReason?: string;
};

export type ConfirmAllocationPayload = {
  confirmedQty: number;
  discrepancyReason?: string;
};
export type ConfirmOnBehalfPayload = ConfirmAllocationPayload & { onBehalfReason: string };
