import type { PageParams } from '@/types/api';

export type WorkerPieceMovement = {
  workerPieceMovementId: number;
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
  operationId: number | null;
  operationName: string | null;
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

export type TeamHoldingLine = {
  orderLineId: number;
  orderCode: string;
  productCode: string;
  color: string;
  sizeCode: string;
  heldByTeam: number;
  pendingHandoverQty: number;
  availableQty: number;
};

export type WorkerHoldingLine = {
  workerId: number;
  workerCode: string;
  workerName: string;
  orderLineId: number;
  orderCode: string;
  productCode: string;
  color: string;
  sizeCode: string;
  heldQty: number;
  pendingRetrievalQty: number;
  retrievableQty: number;
};

export type PieceMovementHoldings = { team: TeamHoldingLine[]; workers: WorkerHoldingLine[] };

export type MovementQuery = PageParams & {
  status?: string;
  movementType?: string;
  workerId?: number;
};

export type CreateMovementPayload = {
  movementType: string;
  workerId: number;
  orderLineId: number;
  operationId?: number;
  quantity: number;
  retrievalReason?: string;
};

export type ConfirmMovementPayload = { confirmedQty: number; discrepancyReason?: string };
export type ConfirmOnBehalfPayload = ConfirmMovementPayload & { onBehalfReason: string };
